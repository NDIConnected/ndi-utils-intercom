using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace NDIIntercom.Core.Linux
{
    /// <summary>
    /// Lists PulseAudio sources/sinks via <c>pactl</c> (PipeWire provides the same CLI).
    /// </summary>
    internal static class PactlDeviceList
    {
        internal static List<(string Name, string Description, bool Running)> ListSources()
        {
            List<(string Name, string Description, bool Running)> list;
            if (TryListJson(isSink: false, omitSinkMonitors: true, out list) && list.Count > 0)
            {
                return list;
            }

            return StripSinkMonitorSources(ParseShortList(isSink: false));
        }

        internal static List<(string Name, string Description, bool Running)> ListSinks()
        {
            if (TryListJson(isSink: true, omitSinkMonitors: false, out var list) && list.Count > 0)
            {
                return list;
            }

            return ParseShortList(isSink: true);
        }

        /// <summary>
        /// Sink monitor sources capture playback (loopback); they are not microphones.
        /// PipeWire names them with a <c>.monitor</c> suffix; JSON exposes <c>device.class=monitor</c>.
        /// </summary>
        private static List<(string Name, string Description, bool Running)> StripSinkMonitorSources(
            List<(string Name, string Description, bool Running)> list)
        {
            return list.FindAll(e => !IsSinkMonitorSource(e.Name, e.Description));
        }

        private static bool IsSinkMonitorSource(string name, string description)
        {
            if (name.EndsWith(".monitor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // English Pulse/pipewire-pulse description; name suffix is the reliable signal.
            if (description.StartsWith("Monitor of ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool TryListJson(bool isSink, bool omitSinkMonitors, out List<(string Name, string Description, bool Running)> result)
        {
            result = new List<(string, string, bool)>();
            string kind = isSink ? "sinks" : "sources";
            string output = RunPactl($"-f json list {kind}");
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(output);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement el in root.EnumerateArray())
                    {
                        AddEntry(el, result, omitSinkMonitors);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(kind, out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement el in arr.EnumerateArray())
                    {
                        AddEntry(el, result, omitSinkMonitors);
                    }
                }

                return result.Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static void AddEntry(JsonElement el, List<(string Name, string Description, bool Running)> result, bool omitSinkMonitors)
        {
            if (!el.TryGetProperty("name", out JsonElement nameEl))
            {
                return;
            }

            string name = nameEl.GetString() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (omitSinkMonitors &&
                el.TryGetProperty("properties", out JsonElement props) &&
                props.TryGetProperty("device.class", out JsonElement dc) &&
                string.Equals(dc.GetString(), "monitor", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string description = name;
            if (el.TryGetProperty("description", out JsonElement descEl))
            {
                string? d = descEl.GetString();
                if (!string.IsNullOrWhiteSpace(d))
                {
                    description = d!;
                }
            }

            bool running = true;
            if (el.TryGetProperty("state", out JsonElement stateEl))
            {
                string? st = stateEl.GetString();
                running = !string.Equals(st, "SUSPENDED", StringComparison.OrdinalIgnoreCase);
            }

            if (omitSinkMonitors && IsSinkMonitorSource(name, description))
            {
                return;
            }

            result.Add((name, description, running));
        }

        private static List<(string Name, string Description, bool Running)> ParseShortList(bool isSink)
        {
            var result = new List<(string, string, bool)>();
            string kind = isSink ? "sinks" : "sources";
            string output = RunPactl($"list short {kind}");
            if (string.IsNullOrWhiteSpace(output))
            {
                return result;
            }

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                string[] parts = trimmed.Split('\t');
                if (parts.Length < 2)
                {
                    continue;
                }

                string name = parts[1].Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                bool running = parts.Length < 5 || !string.Equals(parts[^1].Trim(), "SUSPENDED", StringComparison.OrdinalIgnoreCase);
                result.Add((name, name, running));
            }

            return result;
        }

        private static string RunPactl(string arguments)
        {
            string? pactlPath = PulseAudioBinaries.Pactl;
            if (string.IsNullOrEmpty(pactlPath))
            {
                return "";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pactlPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                {
                    return "";
                }

                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return stdout;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
