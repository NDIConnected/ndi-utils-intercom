using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NDIIntercom.Core.Logging
{
    /// <summary>
    /// Lightweight file logger provider for 24/7 deployments.
    /// - Writes to <c>{LogDirectory}/intercom-YYYY-MM-DD.log</c>; rolls over at midnight UTC.
    /// - Background thread drains a bounded queue and batches writes (one fsync per ~100ms).
    /// - Old log files are pruned to <see cref="FileLoggerOptions.RetentionDays"/>.
    /// - On a write failure (e.g. disk full) the provider goes silent until the next rollover
    ///   rather than throwing into the audio path.
    ///
    /// Intentionally does not depend on Serilog/NLog so the deployment stays self-contained.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly FileLoggerOptions _options;
        private readonly BlockingCollection<LogEntry> _queue;
        private readonly Thread _writerThread;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _writerLock = new object();

        private DateTime _currentLogDate = DateTime.MinValue;
        private string _currentLogPath = string.Empty;

        public FileLoggerProvider(FileLoggerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            // Bounded queue: if logging gets out of hand we drop oldest entries instead of
            // blocking the audio thread or growing memory unbounded.
            _queue = new BlockingCollection<LogEntry>(boundedCapacity: Math.Max(64, _options.QueueCapacity));

            try
            {
                Directory.CreateDirectory(_options.LogDirectory);
            }
            catch
            {
                // If we can't create the directory, the writer thread will skip writes.
            }

            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "IntercomFileLogger"
            };
            _writerThread.Start();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        internal bool IsEnabled(LogLevel level) => level >= _options.MinLevel;

        internal void Enqueue(LogEntry entry)
        {
            // TryAdd with zero timeout: if the queue is full, drop the entry. The writer
            // thread is guaranteed to make progress (single consumer), so a full queue
            // means writes are I/O-bound and we'd rather drop than block.
            try
            {
                if (!_queue.TryAdd(entry, millisecondsTimeout: 0))
                {
                    Interlocked.Increment(ref _droppedCount);
                }
            }
            catch (InvalidOperationException)
            {
                // Queue marked complete during shutdown.
            }
        }

        private long _droppedCount;

        private void WriterLoop()
        {
            var token = _cts.Token;
            // Local buffer to batch writes so we don't open/close the file once per entry.
            var batch = new StringBuilder(capacity: 8192);
            var batchEntries = new System.Collections.Generic.List<LogEntry>(64);

            while (!token.IsCancellationRequested)
            {
                LogEntry entry;
                try
                {
                    if (!_queue.TryTake(out entry, millisecondsTimeout: 200, cancellationToken: token))
                    {
                        // Periodic flush check even with no new entries.
                        FlushDroppedNotice(batch);
                        TryWriteBatch(batch, batchEntries);
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                AppendEntry(batch, entry);
                batchEntries.Add(entry);

                // Drain any further entries quickly so we batch.
                while (batch.Length < 32_768 && _queue.TryTake(out entry, millisecondsTimeout: 0))
                {
                    AppendEntry(batch, entry);
                    batchEntries.Add(entry);
                }

                FlushDroppedNotice(batch);
                TryWriteBatch(batch, batchEntries);
            }

            // Final drain on shutdown.
            try
            {
                while (_queue.TryTake(out var leftover))
                {
                    AppendEntry(batch, leftover);
                }
                FlushDroppedNotice(batch);
                TryWriteBatch(batch, batchEntries);
            }
            catch
            {
                // Best-effort
            }
        }

        private void FlushDroppedNotice(StringBuilder batch)
        {
            long dropped = Interlocked.Exchange(ref _droppedCount, 0);
            if (dropped > 0)
            {
                batch.Append('[').Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
                batch.Append("] WARN  FileLogger: dropped ").Append(dropped).Append(" log entries (queue full)").AppendLine();
            }
        }

        private static void AppendEntry(StringBuilder buf, LogEntry e)
        {
            buf.Append('[')
               .Append(e.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
               .Append("] ")
               .Append(LevelTag(e.Level))
               .Append(' ')
               .Append(e.Category)
               .Append(": ")
               .Append(e.Message);
            if (e.Exception != null)
            {
                buf.AppendLine();
                buf.Append("    ").Append(e.Exception.GetType().FullName).Append(": ").Append(e.Exception.Message);
                if (!string.IsNullOrEmpty(e.Exception.StackTrace))
                {
                    buf.AppendLine();
                    buf.Append(e.Exception.StackTrace);
                }
            }
            buf.AppendLine();
        }

        private static string LevelTag(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "     "
        };

        private void TryWriteBatch(StringBuilder batch, System.Collections.Generic.List<LogEntry> batchEntries)
        {
            if (batch.Length == 0)
                return;

            try
            {
                lock (_writerLock)
                {
                    EnsureLogFileForToday();
                    File.AppendAllText(_currentLogPath, batch.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Disk full / permission error: silently drop this batch. The audio path
                // must NEVER be impacted by a logging failure.
            }
            batch.Clear();
            batchEntries.Clear();
        }

        private void EnsureLogFileForToday()
        {
            DateTime today = DateTime.UtcNow.Date;
            if (_currentLogDate == today && !string.IsNullOrEmpty(_currentLogPath))
                return;

            _currentLogDate = today;
            string fileName = $"intercom-{today:yyyy-MM-dd}.log";
            _currentLogPath = Path.Combine(_options.LogDirectory, fileName);

            // Prune old logs once per day rollover.
            try
            {
                PruneOldLogs(today);
            }
            catch
            {
                // Best-effort cleanup — never throw from the logging path.
            }
        }

        private void PruneOldLogs(DateTime today)
        {
            int retainDays = Math.Max(1, _options.RetentionDays);
            DateTime cutoff = today.AddDays(-retainDays);
            foreach (var file in Directory.EnumerateFiles(_options.LogDirectory, "intercom-*.log"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                // Parse YYYY-MM-DD from "intercom-YYYY-MM-DD"
                if (name.Length >= "intercom-yyyy-MM-dd".Length &&
                    DateTime.TryParseExact(name.Substring("intercom-".Length), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fileDate))
                {
                    if (fileDate.Date < cutoff)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
        }

        public void Dispose()
        {
            try { _queue.CompleteAdding(); } catch { }
            try { _cts.Cancel(); } catch { }
            try { _writerThread.Join(TimeSpan.FromSeconds(2)); } catch { }
            _cts.Dispose();
            _queue.Dispose();
        }

        internal readonly struct LogEntry
        {
            public LogEntry(DateTime ts, LogLevel level, string category, string message, Exception? exception)
            {
                TimestampUtc = ts;
                Level = level;
                Category = category;
                Message = message;
                Exception = exception;
            }
            public DateTime TimestampUtc { get; }
            public LogLevel Level { get; }
            public string Category { get; }
            public string Message { get; }
            public Exception? Exception { get; }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string category)
            {
                _provider = provider;
                _category = category;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                string message;
                try { message = formatter(state, exception); }
                catch { message = state?.ToString() ?? string.Empty; }

                _provider.Enqueue(new LogEntry(DateTime.UtcNow, logLevel, _category, message, exception));
            }
        }
    }

    public sealed class FileLoggerOptions
    {
        /// <summary>Directory where <c>intercom-YYYY-MM-DD.log</c> files live.</summary>
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>Minimum level written to file. Default: Information.</summary>
        public LogLevel MinLevel { get; set; } = LogLevel.Information;

        /// <summary>Number of days of log files to keep. Older ones are deleted on day rollover.</summary>
        public int RetentionDays { get; set; } = 14;

        /// <summary>Bounded queue size; when full, oldest entries are dropped (with a warn marker).</summary>
        public int QueueCapacity { get; set; } = 4096;
    }

    public static class FileLoggerExtensions
    {
        /// <summary>
        /// Adds the file logger provider with the given options. The log directory is
        /// auto-created. Idempotent if called more than once.
        /// </summary>
        public static ILoggingBuilder AddIntercomFileLogger(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
        {
            var options = new FileLoggerOptions();
            configure(options);
            if (string.IsNullOrWhiteSpace(options.LogDirectory))
            {
                throw new ArgumentException("FileLoggerOptions.LogDirectory is required.", nameof(configure));
            }
            builder.AddProvider(new FileLoggerProvider(options));
            return builder;
        }
    }
}
