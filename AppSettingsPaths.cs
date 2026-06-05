using NDIIntercom.Models;

namespace NDIIntercom;

/// <summary>
/// Bundled appsettings.json under Program Files is read-only after install.
/// User overrides (web server bind, port, etc.) are stored under ProgramData.
/// </summary>
public static class AppSettingsPaths
{
    public static string InstallSettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static string UserSettingsDirectory =>
        OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                IntercomRuntime.Product.DataFolderName)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NDI",
                IntercomRuntime.Product.DataFolderName);

    public static string UserSettingsPath =>
        Path.Combine(UserSettingsDirectory, "appsettings.json");
}
