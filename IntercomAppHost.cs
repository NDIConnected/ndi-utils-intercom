#if WINDOWS
using System.Windows.Forms;
#endif
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NDIIntercom.Core;
using NDIIntercom.Core.Logging;
#if !WINDOWS
using NDIIntercom.Core.Linux;
#endif
using NDIIntercom.Hubs;
using NDIIntercom.Models;

namespace NDIIntercom;

/// <summary>
/// Shared startup for full (16ch) and light (2ch) executables. Set <see cref="IntercomRuntime.Product"/> first.
/// </summary>
public static class IntercomAppHost
{
    public static void Run(string[] args, IntercomProductOptions product)
    {
        IntercomRuntime.Product = product;

#if !WINDOWS
        LinuxPulseCompat.EnsureRuntimeEnvironment();
#endif

        var webBinding = WebServerBindingOptions.LoadFromAppsettings(
            IntercomRuntime.Product.DefaultWebPort);
        int port = webBinding.Port;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Configuration.AddJsonFile(AppSettingsPaths.UserSettingsPath, optional: true, reloadOnChange: false);

        // Persistent file logger — required for 24/7 troubleshooting. Lives next to the
        // per-product config directory (%ProgramData%\NDI Intercom16\logs on Windows,
        // ~/.local/share/NDI/NDI Intercom16/logs on Linux). Daily rollover, bounded queue,
        // never blocks the audio path.
        string logDirectory = ResolveLogDirectory();
        builder.Logging.AddIntercomFileLogger(opts =>
        {
            opts.LogDirectory = logDirectory;
            opts.MinLevel = LogLevel.Information;
            opts.RetentionDays = 14;
        });

        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            });
        builder.Services.AddSingleton<IntercomEngine>();
        builder.Services.AddHostedService<NDIIntercom.Hubs.VUMeterBackgroundService>();

        builder.Services.AddSingleton<NDIDiscoveryService>();
        builder.Services.AddHostedService<NDIDiscoveryService>(provider => provider.GetRequiredService<NDIDiscoveryService>());

        builder.Services.AddSingleton<NDISenderDiscoveryService>();
        builder.Services.AddHostedService<NDISenderDiscoveryService>(provider => provider.GetRequiredService<NDISenderDiscoveryService>());

        builder.Services.AddControllers();
        builder.Services.AddHttpClient();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin))
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                        || uri.Host == "127.0.0.1"
                        || uri.Host == "[::1]")
                    {
                        return true;
                    }

                    if (webBinding.RemoteAccess == WebServerRemoteAccess.Interface
                        && !string.IsNullOrWhiteSpace(webBinding.BindAddress)
                        && uri.Host == webBinding.BindAddress)
                    {
                        return true;
                    }

                    return webBinding.RemoteAccess == WebServerRemoteAccess.All
                        && IsPrivateOrLocalOriginHost(uri.Host);
                })
                .AllowAnyMethod()
                .AllowAnyHeader();
            });
        });

        builder.WebHost.ConfigureKestrel(options => webBinding.ConfigureKestrel(options));

        var app = builder.Build();

        // Wire static loggers used by classes that aren't directly DI-injected (NDIManager
        // is created from inside IntercomEngine; ConfigManager is a static API). Doing this
        // here, immediately after Build(), means every subsequent log call lands in our
        // rolling file (and the framework's console).
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        NDIManager.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.NDIManager"));
        ConfigManager.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.ConfigManager"));
        PresetManager.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.PresetManager"));
#if !WINDOWS
        PactlDeviceList.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.Linux.PactlDeviceList"));
#endif
        // Both Windows (WASAPI) and Linux (parec/pacat) AudioEngine implementations expose
        // the same static SetLogger entry point under the same fully-qualified name; the
        // `using` block at the top of each file resolves to the right type per platform.
        NDIIntercom.Core.AudioEngine.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.AudioEngine"));
#if WINDOWS
        // Without this the entire ASIO path is silent in the log: driver init, start failures
        // and control-panel reset requests all used to be swallowed.
        AsioManager.SetLogger(loggerFactory.CreateLogger("NDIIntercom.Core.AsioManager"));
#endif

        var intercomEngine = app.Services.GetRequiredService<IntercomEngine>();

        var startupLogger = loggerFactory.CreateLogger("NDIIntercom.Startup");
        switch (webBinding.RemoteAccess)
        {
            case WebServerRemoteAccess.Off:
                startupLogger.LogInformation(
                    "Web UI on http://127.0.0.1:{Port} (localhost). Enable remote control from tray: Web Server Settings.",
                    port);
                break;
            case WebServerRemoteAccess.Interface:
                startupLogger.LogWarning(
                    "Web UI on http://127.0.0.1:{Port} and http://{BindAddress}:{Port} (selected interface). No authentication — restrict network access.",
                    port,
                    webBinding.BindAddress,
                    port);
                break;
            case WebServerRemoteAccess.All:
                startupLogger.LogWarning(
                    "Web UI on http://127.0.0.1:{Port} and all network interfaces. No authentication — restrict network access.",
                    port);
                break;
        }

        _ = Task.Run(() =>
        {
            try
            {
                // Load the config *before* initializing NDI so senders and receivers are
                // created with their final identity, instead of being recreated as soon as
                // the first ApplyConfig lands.
                var savedConfig = ConfigManager.LoadConfig();

                if (!intercomEngine.Initialize(port, savedConfig))
                {
                    startupLogger.LogCritical("IntercomEngine.Initialize returned false. Audio is non-functional. Investigate the NDI runtime / SDK installation.");
                    return;
                }

                if (savedConfig != null && savedConfig.Channels != null && savedConfig.Channels.Count > 0)
                {
                    intercomEngine.ApplyConfig(savedConfig, false);
                }

                intercomEngine.Start();
                startupLogger.LogInformation("IntercomEngine started on port {Port}", port);
            }
            catch (Exception ex)
            {
                startupLogger.LogCritical(ex, "IntercomEngine startup failed");
            }
        });

        app.UseStaticFiles();
        app.UseCors();
        app.UseRouting();

        app.MapControllers();
        app.MapHub<IntercomHub>("/intercomHub");

        app.MapGet("/", () => Results.Redirect("/index.html"));

        // Health endpoint for 24/7 monitoring. Returns 200 once the NDI engine is up and
        // the dedicated audio thread is running; 503 otherwise so an external probe (or a
        // management dashboard) can tell whether the intercom is actually functional.
        // Intentionally does NOT load config from disk on every call — that would let a
        // health-check loop hammer the filesystem.
        app.MapGet("/healthz", (IntercomEngine engine) =>
        {
            bool initialized = engine.IsInitialized;
            bool running = engine.IsRunning;

            // Receiver counts make an all-silent intercom visible: the engine can be
            // initialized and running while every configured NDI source is unresolved, which
            // is exactly the failure that used to require a manual Settings→Apply.
            var (configuredReceivers, connectedReceivers) = engine.GetNdiReceiverHealth();
            bool receiversOk = connectedReceivers == configuredReceivers;
            bool healthy = initialized && running && receiversOk;

            var payload = new
            {
                product = IntercomRuntime.Product.ProductDisplayName,
                version = typeof(IntercomAppHost).Assembly.GetName().Version?.ToString() ?? "unknown",
                initialized,
                running,
                configuredReceivers,
                connectedReceivers,
                healthy,
            };
            return Results.Json(payload, statusCode: healthy ? 200 : 503);
        });

#if WINDOWS
        _ = app.RunAsync();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext(app, port));
#else
        Console.WriteLine($"{IntercomRuntime.Product.ProductDisplayName} — http://127.0.0.1:{port}  (Ctrl+C to exit)");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            app.Lifetime.StopApplication();
        };
        app.Run();
#endif
    }

    /// <summary>
    /// Resolves the log directory for the active product. Lives next to the JSON config
    /// (Windows: %ProgramData%\NDI Intercom16\logs, Linux: ~/.local/share/NDI/NDI Intercom16/logs).
    /// </summary>
    private static string ResolveLogDirectory() => ConfigManager.LogsDirectory;

    private static bool IsPrivateOrLocalOriginHost(string host)
    {
        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false
        };
    }
}
