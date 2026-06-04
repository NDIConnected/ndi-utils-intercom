using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;

namespace NDIIntercom;

public enum WebServerRemoteAccess
{
    Off,
    Interface,
    All
}

/// <summary>
/// Parses web-server bind settings. Localhost is always enabled; remote access is optional
/// on a selected interface or on all interfaces.
/// </summary>
public sealed class WebServerBindingOptions
{
    public const string AllowedHostsLocalOnly = "localhost;127.0.0.1;[::1]";

    public int Port { get; init; }
    public WebServerRemoteAccess RemoteAccess { get; init; } = WebServerRemoteAccess.Off;
    public string? BindAddress { get; init; }

    public static WebServerBindingOptions FromConfiguration(IConfiguration configuration, int defaultPort)
    {
        int port = configuration.GetValue<int?>("WebServer:Port") ?? defaultPort;
        string? remoteAccessRaw = configuration.GetValue<string>("WebServer:RemoteAccess");
        string? bindAddress = configuration.GetValue<string>("WebServer:BindAddress");
        bool bindLocalhostOnly = configuration.GetValue<bool?>("WebServer:BindLocalhostOnly") ?? true;

        WebServerRemoteAccess remoteAccess;
        if (!string.IsNullOrWhiteSpace(remoteAccessRaw)
            && Enum.TryParse(remoteAccessRaw, ignoreCase: true, out WebServerRemoteAccess parsed))
        {
            remoteAccess = parsed;
        }
        else
        {
            remoteAccess = bindLocalhostOnly ? WebServerRemoteAccess.Off : WebServerRemoteAccess.All;
            if (!bindLocalhostOnly
                && !string.IsNullOrWhiteSpace(bindAddress)
                && bindAddress != "0.0.0.0"
                && bindAddress != "*")
            {
                remoteAccess = WebServerRemoteAccess.Interface;
            }
        }

        if (remoteAccess == WebServerRemoteAccess.Interface && string.IsNullOrWhiteSpace(bindAddress))
        {
            remoteAccess = WebServerRemoteAccess.All;
        }

        return new WebServerBindingOptions
        {
            Port = port,
            RemoteAccess = remoteAccess,
            BindAddress = string.IsNullOrWhiteSpace(bindAddress) ? null : bindAddress.Trim()
        };
    }

    public static WebServerBindingOptions FromAppsettingsFile(string appSettingsPath, int defaultPort)
    {
        if (!File.Exists(appSettingsPath))
        {
            return new WebServerBindingOptions { Port = defaultPort };
        }

        try
        {
            string json = File.ReadAllText(appSettingsPath);
            var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            if (root is not JsonObject obj || obj["WebServer"] is not JsonObject webServer)
            {
                return new WebServerBindingOptions { Port = defaultPort };
            }

            int port = defaultPort;
            if (webServer["Port"] is JsonValue portValue && portValue.TryGetValue(out int savedPort))
            {
                port = savedPort;
            }

            WebServerRemoteAccess remoteAccess = WebServerRemoteAccess.Off;
            if (webServer["RemoteAccess"] is JsonValue remoteValue
                && remoteValue.TryGetValue(out string? remoteAccessRaw)
                && Enum.TryParse(remoteAccessRaw, ignoreCase: true, out WebServerRemoteAccess parsed))
            {
                remoteAccess = parsed;
            }
            else if (webServer["BindLocalhostOnly"] is JsonValue bindValue && bindValue.TryGetValue(out bool bindLocalhostOnly))
            {
                remoteAccess = bindLocalhostOnly ? WebServerRemoteAccess.Off : WebServerRemoteAccess.All;
            }

            string? bindAddress = webServer["BindAddress"]?.GetValue<string>();
            if (remoteAccess == WebServerRemoteAccess.All
                && !string.IsNullOrWhiteSpace(bindAddress)
                && bindAddress != "0.0.0.0"
                && bindAddress != "*"
                && webServer["RemoteAccess"] is null)
            {
                remoteAccess = WebServerRemoteAccess.Interface;
            }

            if (remoteAccess == WebServerRemoteAccess.Interface && string.IsNullOrWhiteSpace(bindAddress))
            {
                remoteAccess = WebServerRemoteAccess.All;
            }

            return new WebServerBindingOptions
            {
                Port = port,
                RemoteAccess = remoteAccess,
                BindAddress = string.IsNullOrWhiteSpace(bindAddress) ? null : bindAddress.Trim()
            };
        }
        catch
        {
            return new WebServerBindingOptions { Port = defaultPort };
        }
    }

    public string ResolveAllowedHosts()
    {
        return RemoteAccess switch
        {
            WebServerRemoteAccess.Off => AllowedHostsLocalOnly,
            WebServerRemoteAccess.All => "*",
            WebServerRemoteAccess.Interface when !string.IsNullOrWhiteSpace(BindAddress)
                => $"{AllowedHostsLocalOnly};{BindAddress.Trim()}",
            _ => AllowedHostsLocalOnly
        };
    }

    public void ConfigureKestrel(KestrelServerOptions options)
    {
        options.ListenLocalhost(Port);

        switch (RemoteAccess)
        {
            case WebServerRemoteAccess.Interface:
                options.Listen(IPAddress.Parse(BindAddress!), Port);
                break;
            case WebServerRemoteAccess.All:
                options.ListenAnyIP(Port);
                break;
        }
    }

    public static IReadOnlyList<NetworkInterfaceOption> ListNetworkInterfaces()
    {
        var results = new List<NetworkInterfaceOption>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork
                    || IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                results.Add(new NetworkInterfaceOption(
                    address.Address.ToString(),
                    $"{nic.Name} — {address.Address}"));
            }
        }

        return results
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void WriteTo(JsonObject webServer, JsonObject root)
    {
        webServer["Port"] = Port;
        webServer["RemoteAccess"] = RemoteAccess.ToString();
        webServer["BindLocalhostOnly"] = RemoteAccess == WebServerRemoteAccess.Off;

        if (RemoteAccess == WebServerRemoteAccess.Interface && !string.IsNullOrWhiteSpace(BindAddress))
        {
            webServer["BindAddress"] = BindAddress;
        }
        else
        {
            webServer.Remove("BindAddress");
        }

        root["AllowedHosts"] = ResolveAllowedHosts();
    }
}

public sealed record NetworkInterfaceOption(string Address, string DisplayName);
