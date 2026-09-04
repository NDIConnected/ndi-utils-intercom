using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Packs the rolling log files, the configuration and a state summary into a single zip
    /// the operator can attach to a support request.
    ///
    /// Exists because reading <c>%ProgramData%\...\logs</c> is impractical for the people who
    /// actually hit the faults: the folder is hidden from normal navigation and the useful
    /// context (which ASIO device came up, how many channels it really has, which NDI sources
    /// resolved) is spread across several places.
    /// </summary>
    public static class DiagnosticsBundle
    {
        /// <summary>
        /// Per-file cap on the log tail written into the bundle. A busy 24/7 day produces tens
        /// of MB; the tail is where the fault is, and the archive has to stay attachable.
        /// </summary>
        private const long MaxLogTailBytes = 4L * 1024 * 1024;

        /// <summary>
        /// Builds the bundle and returns its full path.
        /// </summary>
        /// <param name="engine">Optional running engine; when null the state summary is omitted.</param>
        public static string Create(IntercomEngine engine = null)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string safeProduct = Sanitize(IntercomRuntime.Product.ProductDisplayName);
            string zipName = $"{safeProduct}-diagnostics-{stamp}.zip";

            string destinationDirectory = ResolveDestinationDirectory();
            Directory.CreateDirectory(destinationDirectory);
            string zipPath = Path.Combine(destinationDirectory, zipName);

            // Staged in a temp folder so the archive is assembled from stable copies: the log
            // writer appends to today's file while we work, and config.json can be rewritten
            // by any UI action mid-run.
            string stagingRoot = Path.Combine(Path.GetTempPath(), $"{safeProduct}-diag-{stamp}");
            Directory.CreateDirectory(stagingRoot);

            try
            {
                StageLogs(stagingRoot);
                StageConfiguration(stagingRoot);

                // The logs are the payload that matters; a formatting slip while rendering the
                // state summary must never cost the operator the whole bundle.
                try
                {
                    StageSummary(stagingRoot, engine);
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(stagingRoot, "summary-failed.txt"),
                            $"Could not build the state summary:{Environment.NewLine}{ex}");
                    }
                    catch { }
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                ZipFile.CreateFromDirectory(stagingRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            finally
            {
                try { Directory.Delete(stagingRoot, recursive: true); } catch { }
            }

            return zipPath;
        }

        /// <summary>Directory the bundle is written to: the Desktop, so it is easy to find.</summary>
        private static string ResolveDestinationDirectory()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
                {
                    return desktop;
                }
            }
            catch
            {
                // Roaming profile without a Desktop folder; fall through.
            }

            return Path.Combine(ConfigManager.DataDirectory, "Diagnostics");
        }

        private static void StageLogs(string stagingRoot)
        {
            string logsDirectory = ConfigManager.LogsDirectory;
            if (!Directory.Exists(logsDirectory))
            {
                return;
            }

            string target = Path.Combine(stagingRoot, "logs");
            Directory.CreateDirectory(target);

            foreach (string file in Directory.EnumerateFiles(logsDirectory, "intercom-*.log"))
            {
                try
                {
                    CopyTail(file, Path.Combine(target, Path.GetFileName(file)), MaxLogTailBytes);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(
                        Path.Combine(target, Path.GetFileName(file) + ".unreadable.txt"),
                        $"Could not read this log file: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copies at most <paramref name="maxBytes"/> from the end of a file that another
        /// thread may be appending to. <c>FileShare.ReadWrite</c> is required: the file logger
        /// holds the file for write in short bursts, and a stricter share mode would make this
        /// fail exactly while the system is busy logging a fault.
        /// </summary>
        private static void CopyTail(string sourcePath, string destinationPath, long maxBytes)
        {
            using var source = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            bool truncated = source.Length > maxBytes;
            if (truncated)
            {
                source.Seek(-maxBytes, SeekOrigin.End);
            }

            using var destination = new FileStream(
                destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            if (truncated)
            {
                using var writer = new StreamWriter(destination, new UTF8Encoding(false), 1024, leaveOpen: true);
                writer.WriteLine($"*** truncated: showing the last {maxBytes / (1024 * 1024)} MB of {source.Length / (1024 * 1024)} MB ***");
                writer.Flush();

                // Drop the partial first line so the file starts on an entry boundary.
                SkipToNextLine(source);
            }

            source.CopyTo(destination);
        }

        private static void SkipToNextLine(Stream stream)
        {
            int read;
            while ((read = stream.ReadByte()) != -1)
            {
                if (read == '\n')
                {
                    return;
                }
            }
        }

        private static void StageConfiguration(string stagingRoot)
        {
            TryCopyFile(ConfigManager.ConfigPath, Path.Combine(stagingRoot, "config.json"));

            // Holds the web server port and remote-access mode, which change how the app binds.
            TryCopyFile(AppSettingsPaths.UserSettingsPath, Path.Combine(stagingRoot, "appsettings.user.json"));
        }

        private static void TryCopyFile(string sourcePath, string destinationPath)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return;
                }

                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(destination);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(destinationPath + ".unreadable.txt",
                        $"Could not read {sourcePath}: {ex.GetType().Name}: {ex.Message}");
                }
                catch { }
            }
        }

        private static void StageSummary(string stagingRoot, IntercomEngine engine)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{IntercomRuntime.Product.ProductDisplayName} diagnostics");
            sb.AppendLine($"Generated:        {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local) / {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine($"Version:          {typeof(DiagnosticsBundle).Assembly.GetName().Version?.ToString() ?? "unknown"}");
            sb.AppendLine($"Machine:          {Environment.MachineName}");
            sb.AppendLine($"OS:               {Environment.OSVersion}");
            sb.AppendLine($"64-bit process:   {Environment.Is64BitProcess}");
            sb.AppendLine($"Data directory:   {ConfigManager.DataDirectory}");
            sb.AppendLine();

            if (engine == null)
            {
                sb.AppendLine("Engine state unavailable (the audio engine was not reachable when this bundle was created).");
                WriteSummary(stagingRoot, sb);
                return;
            }

            sb.AppendLine("== Engine ==");
            sb.AppendLine($"Initialized:      {engine.IsInitialized}");
            sb.AppendLine($"Running:          {engine.IsRunning}");
            sb.AppendLine();

            AppendAsioSection(sb, engine);
            AppendChannelSection(sb, engine);

            WriteSummary(stagingRoot, sb);
        }

        private static void AppendAsioSection(StringBuilder sb, IntercomEngine engine)
        {
#if WINDOWS
            sb.AppendLine("== ASIO ==");

            AsioStatus asio;
            try
            {
                asio = engine.GetAsioStatus();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Unavailable: {ex.GetType().Name}: {ex.Message}");
                sb.AppendLine();
                return;
            }

            if (string.IsNullOrEmpty(asio.SelectedDevice))
            {
                sb.AppendLine("No ASIO device selected.");
                sb.AppendLine();
                return;
            }

            sb.AppendLine($"Device:           {asio.SelectedDevice}");
            sb.AppendLine($"Initialized:      {asio.IsInitialized}");
            sb.AppendLine($"Started:          {asio.IsStarted}");
            sb.AppendLine($"Playing:          {asio.IsPlaying}");
            sb.AppendLine($"Engine built:     {asio.EngineBuilt}");
            sb.AppendLine($"Driver inputs:    {asio.InputChannelCount}");
            sb.AppendLine($"Driver outputs:   {asio.OutputChannelCount}");
            if (!string.IsNullOrEmpty(asio.LastError))
            {
                sb.AppendLine($"Last error:       {asio.LastError}");
            }
            if (asio.IsFaulted)
            {
                sb.AppendLine("STATUS:           FAULTED - a device is selected but audio is not flowing.");
            }
            sb.AppendLine();
#endif
        }

        private static void AppendChannelSection(StringBuilder sb, IntercomEngine engine)
        {
            List<ChannelState> channels;
            try
            {
                channels = engine.GetChannels();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"== Channels ==");
                sb.AppendLine($"Unavailable: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            int asioInputs = 0;
            int asioOutputs = 0;
#if WINDOWS
            try
            {
                var asio = engine.GetAsioStatus();
                asioInputs = asio.InputChannelCount;
                asioOutputs = asio.OutputChannelCount;
            }
            catch { }
#endif

            Dictionary<int, NDIReceiverStatus> receiverStatuses;
            try
            {
                receiverStatuses = engine.GetReceiverStatuses();
            }
            catch
            {
                receiverStatuses = new Dictionary<int, NDIReceiverStatus>();
            }

            sb.AppendLine("== Channels ==");
            sb.AppendLine("Ch  Mode  Grp  Talk  Lstn  Routing / source                              Status");
            sb.AppendLine(new string('-', 104));

            foreach (var channel in channels.OrderBy(c => c.ChannelNumber))
            {
                string mode = channel.Mode == ChannelMode.ASIO ? "ASIO" : "NDI ";
                string routing;
                string status;

                if (channel.Mode == ChannelMode.ASIO)
                {
                    routing = $"in {channel.AsioInputChannel + 1} / out {channel.AsioOutputChannel + 1}";

                    var problems = new List<string>();
                    if (asioInputs > 0 && channel.AsioInputChannel >= asioInputs)
                        problems.Add($"input {channel.AsioInputChannel + 1} > device max {asioInputs}");
                    if (asioOutputs > 0 && channel.AsioOutputChannel >= asioOutputs)
                        problems.Add($"output {channel.AsioOutputChannel + 1} > device max {asioOutputs}");

                    status = problems.Count > 0 ? "OUT OF RANGE: " + string.Join("; ", problems) : "ok";
                }
                else
                {
                    routing = string.IsNullOrEmpty(channel.NdiReceiveName) ? "(no source)" : channel.NdiReceiveName;

                    if (string.IsNullOrEmpty(channel.NdiReceiveName))
                    {
                        status = "idle";
                    }
                    else if (receiverStatuses.TryGetValue(channel.ChannelNumber, out var receiver) && receiver.IsConnected)
                    {
                        status = "connected";
                    }
                    else
                    {
                        status = "NOT CONNECTED";
                    }
                }

                sb.Append(channel.ChannelNumber.ToString(CultureInfo.InvariantCulture).PadLeft(2)).Append("  ");
                sb.Append(mode).Append("  ");
                sb.Append(channel.IntercomGroup.ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append("  ");
                sb.Append((channel.TalkEnabled ? "on " : "off").PadRight(4)).Append("  ");
                sb.Append((channel.ListenEnabled ? "on " : "off").PadRight(4)).Append("  ");
                sb.Append(Truncate(routing, 44).PadRight(44)).Append("  ");
                sb.AppendLine(status);
            }

            sb.AppendLine();
        }

        private static void WriteSummary(string stagingRoot, StringBuilder sb)
        {
            File.WriteAllText(Path.Combine(stagingRoot, "summary.txt"), sb.ToString(), new UTF8Encoding(false));
        }

        private static string Truncate(string value, int maxLength)
        {
            value ??= string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 1) + "~";
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '-' : c);
            }
            return sb.ToString();
        }
    }
}
