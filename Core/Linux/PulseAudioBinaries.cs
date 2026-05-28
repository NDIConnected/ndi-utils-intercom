using System;
using System.Collections.Generic;
using System.IO;

namespace NDIIntercom.Core.Linux
{
    /// <summary>
    /// Resolves <c>parec</c>, <c>pacat</c>, and <c>pactl</c> with PATH plus common install locations.
    /// Cursor/SSH/minimal environments often omit <c>/usr/bin</c> from PATH; apphost also inherits a narrow PATH.
    /// </summary>
    internal static class PulseAudioBinaries
    {
        private static string? _parec;
        private static string? _pacat;
        private static string? _pactl;

        internal static string? Parec => _parec ??= FindExecutable("parec");

        internal static string? Pacat => _pacat ??= FindExecutable("pacat");

        internal static string? Pactl => _pactl ??= FindExecutable("pactl");

        internal static string GetParecPathOrThrow()
        {
            return Parec ?? throw new InvalidOperationException(
                "parec not found. Install Pulse utilities: sudo apt install pulseaudio-utils " +
                "(Fedora: pulseaudio-utils; Arch: libpulse). Ensure /usr/bin is on PATH.");
        }

        internal static string GetPacatPathOrThrow()
        {
            return Pacat ?? throw new InvalidOperationException(
                "pacat not found. Install Pulse utilities: sudo apt install pulseaudio-utils " +
                "(Fedora: pulseaudio-utils; Arch: libpulse). Ensure /usr/bin is on PATH.");
        }

        private static string? FindExecutable(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (fileName.Contains(Path.DirectorySeparatorChar) ||
                fileName.Contains('/', StringComparison.Ordinal))
            {
                return File.Exists(fileName) ? fileName : null;
            }

            foreach (string dir in GetSearchDirectories())
            {
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Invalid path segment from PATH
                }
            }

            return null;
        }

        private static IEnumerable<string> GetSearchDirectories()
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                foreach (string entry in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = entry.Trim();
                    if (trimmed.Length > 0)
                    {
                        yield return trimmed;
                    }
                }
            }

            // Typical distro layouts (also covers stripped PATH)
            yield return "/usr/bin";
            yield return "/bin";
            yield return "/usr/local/bin";
        }
    }
}
