using System.Diagnostics;
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
    private readonly NotifyIcon _notifyIcon;
    private readonly int _port;
    private readonly WebApplication _webApp;
    private readonly string _appSettingsPath;

    public TrayApplicationContext(WebApplication webApp, int port)
    {
        _webApp = webApp;
        _port = port;
        _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // Build context menu
        var contextMenu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem($"Open Web Interface (port {_port})");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += OnOpenWebInterface;

        var portItem = new ToolStripMenuItem("Configure Port...");
        portItem.Click += OnConfigurePort;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(portItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        // Load tray icon
        Icon trayIcon = LoadTrayIcon();

        // Create NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            ContextMenuStrip = contextMenu,
            Text = $"{IntercomRuntime.Product.ProductDisplayName} - http://localhost:{_port}",
            Visible = true
        };

        // Double-click opens the web interface
        _notifyIcon.DoubleClick += OnOpenWebInterface;
    }

    /// <summary>
    /// Loads the tray icon from the application directory.
    /// Falls back to a default system icon if the file is not found.
    /// </summary>
    private Icon LoadTrayIcon()
    {
        // Look for app.ico next to the executable
        string icoPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            try
            {
                // Load at 32x32 for better visibility in modern system trays
                return new Icon(icoPath, new System.Drawing.Size(32, 32));
            }
            catch
            {
                // Fall through to default
            }
        }

        // Fallback: extract the application's own icon
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

        // Last resort: use SystemIcons.Application
        return SystemIcons.Application;
    }

    /// <summary>
    /// Opens the web interface in the default browser.
    /// </summary>
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

    /// <summary>
    /// Shows a dialog to configure the web server port.
    /// The new port is saved to appsettings.json and requires a restart.
    /// </summary>
    private void OnConfigurePort(object? sender, EventArgs e)
    {
        using var dialog = new Form
        {
            Text = $"Configure Port - {IntercomRuntime.Product.ProductDisplayName}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            Width = 350,
            Height = 180,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Text = "Web server port:",
            Left = 20,
            Top = 20,
            Width = 200
        };

        var textBox = new TextBox
        {
            Text = _port.ToString(),
            Left = 20,
            Top = 45,
            Width = 290
        };

        var okButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Left = 130,
            Top = 85,
            Width = 80
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 220,
            Top = 85,
            Width = 80
        };

        dialog.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            if (int.TryParse(textBox.Text.Trim(), out int newPort) && newPort >= 1 && newPort <= 65535)
            {
                if (newPort == _port)
                    return;

                try
                {
                    SavePortToSettings(newPort);

                    MessageBox.Show(
                        $"Port changed to {newPort}.\n\nPlease restart {IntercomRuntime.Product.ProductDisplayName} for the change to take effect.",
                        IntercomRuntime.Product.ProductDisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to save port configuration:\n{ex.Message}",
                        IntercomRuntime.Product.ProductDisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show(
                    "Invalid port number. Please enter a value between 1 and 65535.",
                    IntercomRuntime.Product.ProductDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// Saves the new port to appsettings.json, preserving all other settings.
    /// </summary>
    private void SavePortToSettings(int newPort)
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

        if (root is JsonObject obj)
        {
            // Ensure WebServer section exists
            if (obj["WebServer"] is not JsonObject webServer)
            {
                webServer = new JsonObject();
                obj["WebServer"] = webServer;
            }

            webServer["Port"] = newPort;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_appSettingsPath, root.ToJsonString(options));
        }
    }

    /// <summary>
    /// Gracefully shuts down the web host and exits the application.
    /// </summary>
    private async void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        try
        {
            // Stop the host with a bounded timeout so a stuck shutdown doesn't hang the tray exit.
            // The host's own Dispose chain (DI container) takes care of disposing IntercomEngine,
            // which in turn waits for the audio thread to exit before tearing down NDI handles.
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

    /// <summary>
    /// Cleanup on dispose.
    /// </summary>
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
