using System;
using System.IO;

namespace NDIIntercom.Core.Linux
{
    /// <summary>
    /// Headless/SSH/nohup processes often miss session env vars. PipeWire's Pulse shim uses
    /// <c>$XDG_RUNTIME_DIR/pulse/native</c> and may need <c>DBUS_SESSION_BUS_ADDRESS</c>.
    /// </summary>
    internal static class LinuxPulseCompat
    {
        private static bool _applied;

        internal static void EnsureRuntimeEnvironment()
        {
            if (_applied || !OperatingSystem.IsLinux())
            {
                return;
            }

            _applied = true;

            try
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")))
                {
                    string? uid = null;
                    foreach (string line in File.ReadLines("/proc/self/status"))
                    {
                        if (line.StartsWith("Uid:", StringComparison.Ordinal))
                        {
                            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                uid = parts[1];
                            }

                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(uid))
                    {
                        string runUser = Path.Combine("/run/user", uid);
                        if (Directory.Exists(runUser))
                        {
                            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", runUser);
                        }
                    }
                }

                string? xdg = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (string.IsNullOrEmpty(xdg))
                {
                    return;
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULSE_SERVER")))
                {
                    string pulseNative = Path.Combine(xdg, "pulse", "native");
                    if (File.Exists(pulseNative))
                    {
                        Environment.SetEnvironmentVariable("PULSE_SERVER", "unix:" + pulseNative);
                    }
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
                {
                    string bus = Path.Combine(xdg, "bus");
                    if (File.Exists(bus))
                    {
                        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=" + bus);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
