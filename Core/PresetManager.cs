using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    public class PresetManager
    {
        private static string PresetsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            IntercomRuntime.Product.DataFolderName,
            "Presets");

        private static string[] LegacyPresetsDirectories => new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), IntercomRuntime.Product.DataFolderName, "Presets")
        };

        private static bool _migrationAttempted;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static void SavePreset(string presetName, AppConfig config)
        {
            try
            {
                EnsureMigrated();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    throw new ArgumentException("Preset name cannot be empty");
                }

                // Sanitize preset name for file system
                string sanitizedName = SanitizeFileName(presetName);
                string filePath = Path.Combine(PresetsDirectory, $"{sanitizedName}.json");

                Directory.CreateDirectory(PresetsDirectory);

                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static AppConfig LoadPreset(string presetName)
        {
            try
            {
                EnsureMigrated();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    throw new ArgumentException("Preset name cannot be empty");
                }

                string sanitizedName = SanitizeFileName(presetName);
                string filePath = Path.Combine(PresetsDirectory, $"{sanitizedName}.json");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Preset not found: {presetName}");
                }

                string json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

                if (config == null)
                {
                    throw new InvalidOperationException($"Failed to load preset: {presetName}");
                }

                config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
                return config;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static List<string> ListPresets()
        {
            try
            {
                EnsureMigrated();

                if (!Directory.Exists(PresetsDirectory))
                {
                    return new List<string>();
                }

                var presetFiles = Directory.GetFiles(PresetsDirectory, "*.json");
                var presetNames = presetFiles
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .OrderBy(name => name)
                    .ToList();

                return presetNames;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public static void DeletePreset(string presetName)
        {
            try
            {
                EnsureMigrated();

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    throw new ArgumentException("Preset name cannot be empty");
                }

                string sanitizedName = SanitizeFileName(presetName);
                string filePath = Path.Combine(PresetsDirectory, $"{sanitizedName}.json");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Preset not found: {presetName}");
                }

                File.Delete(filePath);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            // Remove invalid file name characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(fileName
                .Where(c => !invalidChars.Contains(c))
                .ToArray());

            // Replace spaces with underscores
            sanitized = sanitized.Replace(' ', '_');

            return sanitized;
        }

        private static void EnsureMigrated()
        {
            if (_migrationAttempted)
                return;

            _migrationAttempted = true;

            try
            {
                Directory.CreateDirectory(PresetsDirectory);

                if (Directory.GetFiles(PresetsDirectory, "*.json").Length > 0)
                    return;

                foreach (var legacyDir in LegacyPresetsDirectories)
                {
                    if (!Directory.Exists(legacyDir))
                        continue;

                    foreach (var legacyFile in Directory.GetFiles(legacyDir, "*.json"))
                    {
                        string fileName = Path.GetFileName(legacyFile);
                        string destPath = Path.Combine(PresetsDirectory, fileName);
                        if (!File.Exists(destPath))
                        {
                            File.Copy(legacyFile, destPath, overwrite: false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently continue
            }
        }
    }
}
