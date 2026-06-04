using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NDIIntercom.Core.Linux;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Linux audio capture via <c>parec</c> subprocess (PipeWire 1.0.x has a bug in pa_simple_read;
    /// parec uses the async PulseAudio API internally and works correctly).
    /// Playback via PulseAudio Simple API (pa_simple_write works fine).
    /// </summary>
    public class AudioEngine : ILocalAudioEngine
    {
        private const int SampleRate = 48000;
        private const int CaptureChannels = 2;
        private const int FramesPerRead = 480;

        // Static logger so capture/playback watchdog messages land in the file logger
        // alongside everything else. NullLogger until IntercomAppHost wires it up.
        private static ILogger _logger = NullLogger.Instance;
        public static void SetLogger(ILogger logger) => _logger = logger ?? NullLogger.Instance;

        private NoiseGate _noiseGate;
        private string? _micDevice;
        private string? _speakerDevice;
        private HashSet<string> _allowedInputDeviceIds = new(StringComparer.Ordinal);
        private HashSet<string> _allowedOutputDeviceIds = new(StringComparer.Ordinal);

        private volatile bool _captureRunning;
        private volatile bool _playbackRunning;
        private Thread? _captureThread;
        private Thread? _playbackThread;

        private readonly object _playbackLock = new object();
        private readonly List<byte> _playbackBuffer = new List<byte>(65536);
        private const int MaxPlaybackBufferBytes = SampleRate * sizeof(float) * 2;

        public event EventHandler<float> MicrophoneLevelUpdated;
        public event EventHandler<byte[]> AudioCaptured;

        static AudioEngine()
        {
            LinuxPulseCompat.EnsureRuntimeEnvironment();
        }

        public AudioEngine()
        {
            _noiseGate = new NoiseGate(SampleRate);
        }

        public List<AudioDeviceInfo> GetInputDevices()
        {
            var list = new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo
                {
                    DeviceId = "",
                    FriendlyName = "Default input (PipeWire / PulseAudio)",
                    IsInput = true,
                    IsOutput = false
                }
            };

            foreach ((string name, string description, _) in PactlDeviceList.ListSources())
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                list.Add(new AudioDeviceInfo
                {
                    DeviceId = name,
                    FriendlyName = description,
                    IsInput = true,
                    IsOutput = false
                });
            }

            return list;
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            var list = new List<AudioDeviceInfo>
            {
                new AudioDeviceInfo
                {
                    DeviceId = "",
                    FriendlyName = "Default output (PipeWire / PulseAudio)",
                    IsInput = false,
                    IsOutput = true
                }
            };

            foreach ((string name, string description, _) in PactlDeviceList.ListSinks())
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                list.Add(new AudioDeviceInfo
                {
                    DeviceId = name,
                    FriendlyName = description,
                    IsInput = false,
                    IsOutput = true
                });
            }

            return list;
        }

        public void SelectMicrophone(string deviceId)
        {
            StopCapture();
            RefreshAllowedDeviceIds();
            _micDevice = ValidateDeviceId(deviceId, _allowedInputDeviceIds, "input");
        }

        public void SelectSpeaker(string deviceId)
        {
            StopPlayback();
            RefreshAllowedDeviceIds();
            _speakerDevice = ValidateDeviceId(deviceId, _allowedOutputDeviceIds, "output");
        }

        private string? ValidateDeviceId(string deviceId, HashSet<string> allowedIds, string kind)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            string trimmed = deviceId.Trim();
            if (allowedIds.Count > 0 && !allowedIds.Contains(trimmed))
            {
                _logger.LogWarning("PulseAudio: ignoring unknown {Kind} device {DeviceId}", kind, trimmed);
                return null;
            }

            return trimmed;
        }

        private void RefreshAllowedDeviceIds()
        {
            _allowedInputDeviceIds = GetInputDevices()
                .Select(d => d.DeviceId ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
            _allowedOutputDeviceIds = GetOutputDevices()
                .Select(d => d.DeviceId ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        public void StartCapture()
        {
            StopCapture();
            _captureRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "PulseAudioCapture"
            };
            _captureThread.Start();
        }

        public void StopCapture()
        {
            _captureRunning = false;
            try
            {
                if (_captureProcess != null && !_captureProcess.HasExited)
                {
                    _captureProcess.Kill();
                }
            }
            catch { }

            if (_captureThread != null)
            {
                _captureThread.Join(3000);
                _captureThread = null;
            }
        }

        public void StartPlayback()
        {
            StopPlayback();
            _playbackRunning = true;
            _playbackThread = new Thread(PlaybackLoop)
            {
                IsBackground = true,
                Name = "PulseAudioPlayback"
            };
            _playbackThread.Start();
        }

        public void StopPlayback()
        {
            _playbackRunning = false;
            lock (_playbackLock)
            {
                Monitor.PulseAll(_playbackLock);
            }

            try
            {
                if (_playbackProcess != null && !_playbackProcess.HasExited)
                {
                    _playbackProcess.Kill();
                }
            }
            catch { }

            if (_playbackThread != null)
            {
                _playbackThread.Join(3000);
                _playbackThread = null;
            }

            lock (_playbackLock)
            {
                _playbackBuffer.Clear();
            }
        }

        public void AddToOutputBuffer(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                return;
            }

            lock (_playbackLock)
            {
                _playbackBuffer.AddRange(audioData);
                while (_playbackBuffer.Count > MaxPlaybackBufferBytes)
                {
                    int drop = _playbackBuffer.Count - MaxPlaybackBufferBytes;
                    _playbackBuffer.RemoveRange(0, drop);
                }

                Monitor.Pulse(_playbackLock);
            }
        }

        public void ConfigureNoiseGate(bool enabled, float thresholdDb, float attackMs, float releaseMs, float holdMs)
        {
            _noiseGate.Enabled = enabled;
            _noiseGate.SetThreshold(thresholdDb);
            _noiseGate.SetAttackTime(attackMs);
            _noiseGate.SetReleaseTime(releaseMs);
            _noiseGate.SetHysteresis(6.0f);
            _noiseGate.SetHoldTime(holdMs);
        }

        public bool IsNoiseGateOpen => _noiseGate.CurrentGain > 0.5f;

        private Process? _captureProcess;

        public void Dispose()
        {
            StopCapture();
            StopPlayback();
        }

        // Watchdog parameters mirror the Windows AudioEngine: bounded exponential backoff
        // capped at 16s, retrying forever as long as capture is still desired. This is what
        // lets a 24/7 deployment self-heal across PipeWire/PulseAudio restarts, USB device
        // reconnects, parec process crashes, etc.
        private const int CaptureWatchdogInitialDelayMs = 500;
        private const int CaptureWatchdogMaxDelayMs = 16000;

        private void CaptureLoop()
        {
            int delayMs = CaptureWatchdogInitialDelayMs;

            // Outer loop: keep reviving parec for as long as the user wants capture running.
            // Each successful long-running session resets the backoff; consecutive failures
            // grow the delay exponentially so we don't spin on a permanently-broken setup.
            while (_captureRunning)
            {
                long startTicks = Environment.TickCount64;
                try
                {
                    RunParecCapture();
                    // RunParecCapture returned cleanly (parec exited 0). If the user still
                    // wants capture running, treat it as an unexpected termination and
                    // reschedule a retry — parec dying mid-stream is the typical 24/7 case
                    // (e.g. PipeWire restart).
                }
                catch (Exception ex) when (_captureRunning)
                {
                    _logger.LogWarning(ex, "PulseAudio capture (parec) error; will retry");
                }

                if (!_captureRunning)
                    break;

                // Reset backoff when the previous session ran for a non-trivial time —
                // the device was healthy; this is just a transient hiccup.
                long elapsedMs = Environment.TickCount64 - startTicks;
                if (elapsedMs >= 5000)
                {
                    delayMs = CaptureWatchdogInitialDelayMs;
                }

                try
                {
                    Thread.Sleep(delayMs);
                }
                catch (ThreadInterruptedException)
                {
                    // Interruption is treated as a fast-path retry signal.
                }

                delayMs = Math.Min(delayMs * 2, CaptureWatchdogMaxDelayMs);
            }

            _logger.LogInformation("PulseAudio: capture watchdog exiting (capture stop requested).");
        }

        private void RunParecCapture()
        {
            var psi = new ProcessStartInfo
            {
                FileName = PulseAudioBinaries.GetParecPathOrThrow(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--raw");
            psi.ArgumentList.Add("--format=s16le");
            psi.ArgumentList.Add("--channels=2");
            psi.ArgumentList.Add("--rate=48000");
            psi.ArgumentList.Add("--latency-msec=10");
            if (!string.IsNullOrEmpty(_micDevice))
            {
                psi.ArgumentList.Add($"--device={_micDevice}");
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                throw new InvalidOperationException("Failed to start parec process.");
            }

            _captureProcess = proc;
            _logger.LogInformation("PulseAudio: parec started (pid={Pid}, device={Device}, s16le, 2ch, 48000Hz)", proc.Id, _micDevice ?? "<default>");

            try
            {
                var stream = proc.StandardOutput.BaseStream;
                int bytesPerRead = FramesPerRead * CaptureChannels * sizeof(short);
                var raw = new byte[bytesPerRead];
                var mono = new byte[FramesPerRead * sizeof(float)];

                while (_captureRunning && !proc.HasExited)
                {
                    int totalRead = 0;
                    while (totalRead < bytesPerRead && _captureRunning && !proc.HasExited)
                    {
                        int n = stream.Read(raw, totalRead, bytesPerRead - totalRead);
                        if (n <= 0)
                        {
                            break;
                        }

                        totalRead += n;
                    }

                    if (totalRead < bytesPerRead)
                    {
                        break;
                    }

                    int stereoFrames = FramesPerRead;
                    for (int i = 0; i < stereoFrames; i++)
                    {
                        short sl = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(i * 4));
                        short sr = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(i * 4 + 2));
                        float m = ((sl + sr) * 0.5f) / 32768f;
                        BinaryPrimitives.WriteSingleLittleEndian(mono.AsSpan(i * 4, 4), m);
                    }

                    int monoBytes = stereoFrames * sizeof(float);
                    _noiseGate.Process(mono, monoBytes);
                    // Defensive: subscriber exceptions must not propagate into the capture
                    // loop and tear it down — the watchdog would just relaunch parec, but
                    // we'd burn a needless restart cycle and lose ~1s of audio.
                    try { MicrophoneLevelUpdated?.Invoke(this, CalculateRmsDb(mono, monoBytes)); } catch { }
                    byte[] copy = new byte[monoBytes];
                    Buffer.BlockCopy(mono, 0, copy, 0, monoBytes);
                    try { AudioCaptured?.Invoke(this, copy); } catch { }
                }

                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }

                proc.WaitForExit(2000);

                if (proc.ExitCode != 0 && _captureRunning)
                {
                    string stderr = "";
                    try { stderr = proc.StandardError.ReadToEnd(); } catch { }
                    throw new InvalidOperationException($"parec exited with code {proc.ExitCode}: {stderr.Trim()}");
                }

                _logger.LogInformation("PulseAudio: capture (parec) stopped.");
            }
            finally
            {
                _captureProcess = null;
                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }
            }
        }

        private Process? _playbackProcess;

        private const int PlaybackWatchdogInitialDelayMs = 500;
        private const int PlaybackWatchdogMaxDelayMs = 16000;

        private void PlaybackLoop()
        {
            int delayMs = PlaybackWatchdogInitialDelayMs;

            // Mirrors CaptureLoop: keep reviving pacat as long as playback is desired,
            // with bounded exponential backoff so 24/7 operation survives audio server
            // restarts and device reconnects.
            while (_playbackRunning)
            {
                long startTicks = Environment.TickCount64;
                try
                {
                    RunPacatPlayback();
                }
                catch (Exception ex) when (_playbackRunning)
                {
                    _logger.LogWarning(ex, "PulseAudio playback error; will retry");
                }

                if (!_playbackRunning)
                    break;

                long elapsedMs = Environment.TickCount64 - startTicks;
                if (elapsedMs >= 5000)
                {
                    delayMs = PlaybackWatchdogInitialDelayMs;
                }

                try
                {
                    Thread.Sleep(delayMs);
                }
                catch (ThreadInterruptedException) { }

                delayMs = Math.Min(delayMs * 2, PlaybackWatchdogMaxDelayMs);
            }

            _logger.LogInformation("PulseAudio: playback watchdog exiting (playback stop requested).");
        }

        private void RunPacatPlayback()
        {
            var psi = new ProcessStartInfo
            {
                FileName = PulseAudioBinaries.GetPacatPathOrThrow(),
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--raw");
            psi.ArgumentList.Add("--format=s16le");
            psi.ArgumentList.Add("--channels=2");
            psi.ArgumentList.Add("--rate=48000");
            psi.ArgumentList.Add("--latency-msec=20");
            if (!string.IsNullOrEmpty(_speakerDevice))
            {
                psi.ArgumentList.Add($"--device={_speakerDevice}");
            }

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _logger.LogError("PulseAudio: failed to start pacat process.");
                return;
            }

            _playbackProcess = proc;
            _logger.LogInformation("PulseAudio: pacat playback started (pid={Pid}, device={Device}, s16le, 2ch, 48000Hz)", proc.Id, _speakerDevice ?? "<default>");

            try
            {
                var outStream = proc.StandardInput.BaseStream;
                var chunk = new byte[4096];
                var s16Scratch = new byte[8192];

                while (_playbackRunning && !proc.HasExited)
                {
                    int toWrite = 0;

                    lock (_playbackLock)
                    {
                        while (_playbackBuffer.Count < sizeof(float) && _playbackRunning)
                        {
                            Monitor.Wait(_playbackLock, 50);
                        }

                        if (!_playbackRunning)
                        {
                            break;
                        }

                        toWrite = Math.Min(chunk.Length, _playbackBuffer.Count);
                        if (toWrite == 0)
                        {
                            continue;
                        }

                        _playbackBuffer.CopyTo(0, chunk, 0, toWrite);
                        _playbackBuffer.RemoveRange(0, toWrite);
                    }

                    int monoFrames = toWrite / sizeof(float);
                    int stereoS16Bytes = monoFrames * 2 * sizeof(short);

                    for (int i = 0; i < monoFrames; i++)
                    {
                        float m = BitConverter.ToSingle(chunk, i * sizeof(float));
                        short s = (short)Math.Clamp(m * 32767f, short.MinValue, short.MaxValue);
                        BinaryPrimitives.WriteInt16LittleEndian(s16Scratch.AsSpan(i * 4), s);
                        BinaryPrimitives.WriteInt16LittleEndian(s16Scratch.AsSpan(i * 4 + 2), s);
                    }

                    try
                    {
                        outStream.Write(s16Scratch, 0, stereoS16Bytes);
                        outStream.Flush();
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                try { outStream.Close(); } catch { }

                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }

                proc.WaitForExit(2000);
                _logger.LogInformation("PulseAudio: playback (pacat) stopped.");
            }
            finally
            {
                _playbackProcess = null;
                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                }
            }
        }

        private static float CalculateRmsDb(byte[] buffer, int length)
        {
            if (length <= 0)
            {
                return -60f;
            }

            int sampleCount = length / sizeof(float);
            if (sampleCount == 0)
            {
                return -60f;
            }

            float sum = 0f;
            for (int i = 0; i < length; i += sizeof(float))
            {
                float sample = BitConverter.ToSingle(buffer, i);
                sum += sample * sample;
            }

            float rms = (float)Math.Sqrt(sum / sampleCount);
            if (rms < 1e-10f)
            {
                return -60f;
            }

            return Math.Max(20f * (float)Math.Log10(rms), -60f);
        }
    }
}
