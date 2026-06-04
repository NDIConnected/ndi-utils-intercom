using System;
using System.IO;

namespace NDIIntercom.Core
{
    /// <summary>
    /// Resolves user-supplied file names to paths confined under a base directory.
    /// </summary>
    internal static class SafePath
    {
        public static string ResolveUnderDirectory(string baseDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty.");
            }

            string safeName = Path.GetFileName(fileName.Trim());
            if (string.IsNullOrEmpty(safeName))
            {
                throw new ArgumentException("Invalid file name.");
            }

            if (safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("File name contains invalid characters.");
            }

            Directory.CreateDirectory(baseDirectory);
            string root = Path.GetFullPath(baseDirectory);
            string fullPath = Path.GetFullPath(Path.Combine(root, safeName));

            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Path is outside the allowed directory.");
            }

            return fullPath;
        }
    }
}
