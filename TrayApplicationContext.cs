using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using NDIIntercom.Models;

namespace NDIIntercom;

/// <summary>
/// ApplicationContext that manages the system tray icon and context menu
/// for NDI Intercom16 desktop application.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private const string AllowedHostsLocalOnly = "localhost;127.0.0.1;[::1]";

    private readonly NotifyIcon _notifyIcon;
    private readonly int _port;
    private readonly bool _bindLocalhostOnly;
    private readonly WebApplication _webApp;
    private readonly string _appSettingsPath;

    public TrayApplicationContext(WebApplication webApp, int port)
    {
        _webApp = webApp;
        _port = port;
        _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _bindLocalhostOnly = LoadWebServerSettings().bindLocalhostOnly;

        var contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem($"Open Web Interface (port {_port})");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += OnOpenWebInterface;

        var webServerItem = new ToolStripMenuItem("Web Server Settings...");
        webServerItem.Click += OnConfigureWebServer;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(webServerItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        Icon trayIcon = LoadTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            ContextMenuStrip = contextMenu,
            Text = BuildTrayTooltip(_port, _bindLocalhostOnly),
            Visible = true
        };

        _notifyIcon.DoubleClick += OnOpenWebInterface;
    }

    private static string BuildTrayTooltip(int port, bool bindLocalhostOnly)
    {
        string baseText = $"{IntercomRuntime.Product.ProductDisplayName} - http://localhost:{port}";
        return bindLocalhostOnly ? baseText : $"{baseText} (LAN access enabled)";
    }

    private Icon LoadTrayIcon()
    {
        string icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                return new Icon(icoPath, new System.Drawing.Size(32, 32));
            }
            catch
            {
                // Fall through to default
            }
        }

        string exePath = Environment.ProcessPath ?? "";
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            try
            {
                var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                if (exeIcon != null) return exeIcon;
            }
            catch
            {
                // Fall through to default
            }
        }

        return SystemIcons.Application;
    }

    private void OnOpenWebInterface(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"http://localhost:{_port}",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to open browser:\n{ex.Message}",
                IntercomRuntime.Product.ProductDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OnConfigureWebServer(object? sender, EventArgs e)
    {
        var (_, bindLocalhostOnly) = LoadWebServerSettings();
        bool allowRemote = !bindLocalhostOnly;

        using var dialog = new Form
        {
            Text = $"Web Server Settings - {IntercomRuntime.Product.ProductDisplayName}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            Width = 420,
            Height = 300,
            ShowInTaskbar = false
        };

        var portLabel = new Label
        {
            Text = "Web server port:",
            Left = 20,
            Top = 20,
            Width = 360
        };

        var portTextBox = new TextBox
        {
            Text = _port.ToString(),
            Left = 20,
            Top = 42,
            Width = 360
        };

        var remoteCheckBox = new CheckBox
        {
            Text = "Allow control from other computers on this network",
            Left = 20,
            Top = 78,
            Width = 360,
            Checked = allowRemote
        };

        var warningLabel = new Label
        {
            Text = "No authentication: anyone on the LAN who can reach this port can control the intercom. Ensure Windows Firewall allows inbound TCP on the port.",
            Left = 38,
            Top = 102,
            Width = 342,
            Height = 48,
            ForeColor = System.Drawing.Color.DimGray,
            Visible = allowRemote
        };

        var lanLabel = new Label
        {
            Text = BuildLanUrlsLabel(_port),
            Left = 38,
            Top = 152,
            Width = 342,
            Height = 40,
            Visible = allowRemote
        };

        var okButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Left = 220,
            Top = 210,
            Width = 80
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 310,
            Top = 210,
            Width = 80
        };

        remoteCheckBox.CheckedChanged += (_, _) =>
        {
            warningLabel.Visible = remoteCheckBox.Checked;
            lanLabel.Visible = remoteCheckBox.Checked;
            if (remoteCheckBox.Checked && int.TryParse(portTextBox.Text.Trim(), out int p))
            {
                lanLabel.Text = BuildLanUrlsLabel(p);
            }
        };

        portTextBox.TextChanged += (_, _) =>
        {
            if (remoteCheckBox.Checked && int.TryParse(portTextBox.Text.Trim(), out int p))
            {
                lanLabel.Text = BuildLanUrlsLabel(p);
            }
        };

        dialog.Controls.AddRange(new Control[]
        {
            portLabel, portTextBox, remoteCheckBox, warningLabel, lanLabel, okButton, cancelButton
        });
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (!int.TryParse(portTextBox.Text.Trim(), out int newPort) || newPort < 1 || newPort > 65535)
        {
            MessageBox.Show(
                "Invalid port number. Please enter a value between 1 and 65535.",
                IntercomRuntime.Product.ProductDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        bool newAllowRemote = remoteCheckBox.Checked;
        if (newPort == _port && newAllowRemote == allowRemote)
        {
            return;
        }

        try
        {
            SaveWebServerSettings(newPort, newAllowRemote);

            string message = $"Web server settings saved.\n\nPlease restart {IntercomRuntime.Product.ProductDisplayName} for the changes to take effect.";
            if (newAllowRemote)
            {
                string lanUrls = FormatLanUrlsForMessage(newPort);
                if (!string.IsNullOrEmpty(lanUrls))
                {
                    message += $"\n\nOther computers can use:\n{lanUrls}";
                }
                message += "\n\nEnsure Windows Firewall allows inbound TCP on this port.";
            }

            MessageBox.Show(
                message,
                IntercomRuntime.Product.ProductDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save web server settings:\n{ex.Message}",
                IntercomRuntime.Product.ProductDisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string BuildLanUrlsLabel(int port)
    {
        var urls = GetLocalLanUrls(port);
        if (urls.Count == 0)
        {
            return "LAN URL: use this PC's IP address with the port above.";
        }

        return "LAN URL(s):\n" + string.Join("\n", urls);
    }

    private static string FormatLanUrlsForMessage(int port) =>
        string.Join("\n", GetLocalLanUrls(port));

    private static List<string> GetLocalLanUrls(int port)
    {
        var urls = new List<string>();
        try
        {
            foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    urls.Add($"http://{address}:{port}");
                }
            }
        }
        catch
        {
            // Best-effort hint only
        }

        return urls;
    }

    private (int port, bool bindLocalhostOnly) LoadWebServerSettings()
    {
        int port = _port;
        bool bindLocalhostOnly = true;

        if (!File.Exists(_appSettingsPath))
        {
            return (port, bindLocalhostOnly);
        }

        try
        {
            string json = File.ReadAllText(_appSettingsPath);
            var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            if (root is not JsonObject obj || obj["WebServer"] is not JsonObject webServer)
            {
                return (port, bindLocalhostOnly);
            }

            if (webServer["Port"] is JsonValue portValue && portValue.TryGetValue(out int savedPort))
            {
                port = savedPort;
            }

            if (webServer["BindLocalhostOnly"] is JsonValue bindValue && bindValue.TryGetValue(out bool savedBind))
            {
                bindLocalhostOnly = savedBind;
            }
        }
        catch
        {
            // Use defaults from constructor state
        }

        return (port, bindLocalhostOnly);
    }

    private void SaveWebServerSettings(int port, bool allowRemoteAccess)
    {
        JsonNode? root;

        if (File.Exists(_appSettingsPath))
        {
            string json = File.ReadAllText(_appSettingsPath);
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        else
        {
            root = new JsonObject();
        }

        if (root is not JsonObject obj)
        {
            throw new InvalidOperationException("Invalid appsettings.json structure.");
        }

        if (obj["WebServer"] is not JsonObject webServer)
        {
            webServer = new JsonObject();
            obj["WebServer"] = webServer;
        }

        webServer["Port"] = port;
        webServer["BindLocalhostOnly"] = !allowRemoteAccess;
        obj["AllowedHosts"] = allowRemoteAccess ? "*" : AllowedHostsLocalOnly;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_appSettingsPath, root.ToJsonString(options));
    }

    private async void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _webApp.StopAsync(cts.Token);
            await _webApp.DisposeAsync();
        }
        catch
        {
            // Best-effort shutdown
        }

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
