using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NDIIntercom.Models;

namespace NDIIntercom.Core
{
    public class ConfigManager
    {
        private static string ConfigDirectory =>
            OperatingSystem.IsWindows()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    IntercomRuntime.Product.DataFolderName)
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NDI",
                    IntercomRuntime.Product.DataFolderName);

        private static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

        private static string ExportsDirectory => Path.Combine(ConfigDirectory, "Exports");

        /// <summary>Per-product data directory (config, logs, exports, presets).</summary>
        public static string DataDirectory => ConfigDirectory;

        /// <summary>Full path of <c>config.json</c>.</summary>
        public static string ConfigPath => ConfigFilePath;

        /// <summary>Directory holding the rolling <c>intercom-YYYY-MM-DD.log</c> files.</summary>
        public static string LogsDirectory => Path.Combine(ConfigDirectory, "logs");

        private static string[] LegacyConfigDirectories => new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), IntercomRuntime.Product.DataFolderName)
        };

        private static bool _migrationAttempted;

        // Serializes concurrent SaveConfig calls (UI thread + audio path may both save).
        // The lock also covers the temp-file rename so a reader can never observe a
        // half-written config file.
        private static readonly object _saveLock = new object();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Static logger so the static API surface stays unchanged. IntercomAppHost wires
        // this at startup; a NullLogger is used until then.
        private static ILogger _logger = NullLogger.Instance;
        public static void SetLogger(ILogger logger) => _logger = logger ?? NullLogger.Instance;

        public static AppConfig LoadConfig()
        {
            try
            {
                EnsureMigrated();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                    cfg.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
                    return cfg;
                }

                foreach (var legacyDir in LegacyConfigDirectories)
                {
                    string legacyPath = Path.Combine(legacyDir, "config.json");
                    if (File.Exists(legacyPath))
                    {
                        string json = File.ReadAllText(legacyPath);
                        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                        config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
                        try
                        {
                            SaveConfig(config);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Legacy config migration: SaveConfig failed (legacyPath={LegacyPath})", legacyPath);
                        }
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfigManager.LoadConfig failed (path={ConfigFilePath})", ConfigFilePath);
            }

            var empty = new AppConfig();
            empty.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
            return empty;
        }

        /// <summary>
        /// Deep copy so callers can hand the live configuration to the UI without racing
        /// a concurrent save that clears and rebuilds <see cref="AppConfig.Channels"/>.
        /// </summary>
        public static AppConfig Clone(AppConfig config)
        {
            if (config == null)
            {
                var empty = new AppConfig();
                empty.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
                return empty;
            }

            string json = JsonSerializer.Serialize(config, JsonOptions);
            var copy = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            copy.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
            return copy;
        }

        public static void SaveConfig(AppConfig config)
        {
            // Atomic + thread-safe save:
            //   1. Serialize fully into a string before touching disk so a serialization
            //      error never leaves a partial file behind.
            //   2. Write to a temp file in the same directory, then File.Move with overwrite.
            //      File.Move is atomic on the same volume, so a reader never observes a
            //      half-written file (matters for power-loss / kill-9 scenarios in 24/7).
            //   3. Lock against concurrent writers so two SaveConfig calls don't race on
            //      the temp file.
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string json = JsonSerializer.Serialize(config, JsonOptions);

                lock (_saveLock)
                {
                    string finalPath = ConfigFilePath;
                    string tempPath = finalPath + ".tmp";

                    // If a previous save crashed mid-write, clean up its temp file first.
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

                    File.WriteAllText(tempPath, json);

                    // File.Move with overwrite=true is atomic on the same filesystem; this
                    // guarantees readers see either the old or the new file, never partial.
                    File.Move(tempPath, finalPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                // Surface the failure but never propagate — a transient save error (disk full,
                // permission denied) must not take down the audio path. Operators see this in
                // the rolling log file.
                _logger.LogError(ex, "ConfigManager.SaveConfig failed (path={ConfigFilePath})", ConfigFilePath);
            }
        }

        public static void ExportConfig(AppConfig config, string filePath)
        {
            string resolvedPath = SafePath.ResolveUnderDirectory(ExportsDirectory, filePath);
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(resolvedPath, json);
        }

        public static AppConfig ImportConfig(string filePath)
        {
            string resolvedPath = SafePath.ResolveUnderDirectory(ExportsDirectory, filePath);

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {Path.GetFileName(filePath)}");
            }

            string json = File.ReadAllText(resolvedPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (config == null)
            {
                throw new InvalidOperationException("Failed to deserialize configuration");
            }

            config.EnsureChannelCount(IntercomRuntime.Product.MaxChannels);
            return config;
        }

        private static void EnsureMigrated()
        {
            if (_migrationAttempted)
                return;

            _migrationAttempted = true;

            try
            {
                if (File.Exists(ConfigFilePath))
                    return;

                foreach (var legacyDir in LegacyConfigDirectories)
                {
                    string legacyPath = Path.Combine(legacyDir, "config.json");
                    if (File.Exists(legacyPath))
                    {
                        Directory.CreateDirectory(ConfigDirectory);
                        File.Copy(legacyPath, ConfigFilePath, overwrite: true);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConfigManager.EnsureMigrated failed");
            }
        }
    }
}
