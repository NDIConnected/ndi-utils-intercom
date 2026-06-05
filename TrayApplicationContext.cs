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
    private readonly WebServerBindingOptions _binding;
    private readonly WebApplication _webApp;
    public TrayApplicationContext(WebApplication webApp, int port)
    {
        _webApp = webApp;
        _port = port;
        _binding = WebServerBindingOptions.LoadFromAppsettings(port);

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
            Text = BuildTrayTooltip(_port, _binding),
            Visible = true
        };

        _notifyIcon.DoubleClick += OnOpenWebInterface;
    }

    private static string BuildTrayTooltip(int port, WebServerBindingOptions binding)
    {
        string baseText = $"{IntercomRuntime.Product.ProductDisplayName} - http://localhost:{port}";
        return binding.RemoteAccess switch
        {
            WebServerRemoteAccess.Interface =>
                $"{baseText} (remote on {binding.BindAddress})",
            WebServerRemoteAccess.All =>
                $"{baseText} (remote on all interfaces)",
            _ => baseText
        };
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
                FileName = $"http://127.0.0.1:{_port}",
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
        var current = WebServerBindingOptions.LoadFromAppsettings(_port);
        var interfaces = WebServerBindingOptions.ListNetworkInterfaces();

        using var dialog = new Form
        {
            Text = $"Web Server Settings - {IntercomRuntime.Product.ProductDisplayName}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            Width = 460,
            Height = 390,
            ShowInTaskbar = false
        };

        var portLabel = new Label
        {
            Text = "Web server port:",
            Left = 20,
            Top = 16,
            Width = 400
        };

        var portTextBox = new TextBox
        {
            Text = _port.ToString(),
            Left = 20,
            Top = 38,
            Width = 400
        };

        var remoteLabel = new Label
        {
            Text = "Remote control (localhost is always available on this PC):",
            Left = 20,
            Top = 72,
            Width = 400
        };

        var remoteOffRadio = new RadioButton
        {
            Text = "Disabled",
            Left = 20,
            Top = 98,
            Width = 400,
            Checked = current.RemoteAccess == WebServerRemoteAccess.Off
        };

        var remoteInterfaceRadio = new RadioButton
        {
            Text = "Enabled on selected network interface:",
            Left = 20,
            Top = 124,
            Width = 400,
            Checked = current.RemoteAccess == WebServerRemoteAccess.Interface,
            Enabled = interfaces.Count > 0
        };

        var interfaceCombo = new ComboBox
        {
            Left = 38,
            Top = 148,
            Width = 382,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(NetworkInterfaceOption.DisplayName),
            Enabled = interfaces.Count > 0
        };
        foreach (var nic in interfaces)
        {
            interfaceCombo.Items.Add(nic);
        }

        SelectInterfaceCombo(interfaceCombo, current.BindAddress, interfaces);

        var remoteAllRadio = new RadioButton
        {
            Text = "Enabled on all network interfaces (advanced)",
            Left = 20,
            Top = 182,
            Width = 400,
            Checked = current.RemoteAccess == WebServerRemoteAccess.All
        };

        var warningLabel = new Label
        {
            Text = "No authentication. Remote clients on the chosen network can control the intercom. Allow inbound TCP on the port in Windows Firewall.",
            Left = 38,
            Top = 210,
            Width = 382,
            Height = 48,
            ForeColor = System.Drawing.Color.DimGray
        };

        var remoteUrlLabel = new Label
        {
            Left = 38,
            Top = 262,
            Width = 382,
            Height = 36,
            ForeColor = System.Drawing.Color.DimGray
        };

        var okButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Left = 250,
            Top = 310,
            Width = 80
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 340,
            Top = 310,
            Width = 80
        };

        void UpdateRemoteUi()
        {
            bool remoteEnabled = remoteInterfaceRadio.Checked || remoteAllRadio.Checked;
            interfaceCombo.Enabled = remoteInterfaceRadio.Checked && interfaces.Count > 0;
            warningLabel.Visible = remoteEnabled;
            remoteUrlLabel.Visible = remoteEnabled;
            if (int.TryParse(portTextBox.Text.Trim(), out int previewPort))
            {
                remoteUrlLabel.Text = BuildRemoteUrlPreview(
                    previewPort,
                    remoteOffRadio.Checked ? WebServerRemoteAccess.Off
                        : remoteInterfaceRadio.Checked ? WebServerRemoteAccess.Interface
                        : WebServerRemoteAccess.All,
                    interfaceCombo.SelectedItem as NetworkInterfaceOption);
            }
        }

        remoteOffRadio.CheckedChanged += (_, _) => UpdateRemoteUi();
        remoteInterfaceRadio.CheckedChanged += (_, _) => UpdateRemoteUi();
        remoteAllRadio.CheckedChanged += (_, _) => UpdateRemoteUi();
        portTextBox.TextChanged += (_, _) => UpdateRemoteUi();
        interfaceCombo.SelectedIndexChanged += (_, _) => UpdateRemoteUi();
        UpdateRemoteUi();

        dialog.Controls.AddRange(new Control[]
        {
            portLabel, portTextBox, remoteLabel,
            remoteOffRadio, remoteInterfaceRadio, interfaceCombo, remoteAllRadio,
            warningLabel, remoteUrlLabel, okButton, cancelButton
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

        WebServerRemoteAccess newRemoteAccess = remoteOffRadio.Checked
            ? WebServerRemoteAccess.Off
            : remoteInterfaceRadio.Checked
                ? WebServerRemoteAccess.Interface
                : WebServerRemoteAccess.All;

        string? bindAddress = null;
        if (newRemoteAccess == WebServerRemoteAccess.Interface)
        {
            if (interfaceCombo.SelectedItem is not NetworkInterfaceOption selected)
            {
                MessageBox.Show(
                    "Select a network interface for remote control.",
                    IntercomRuntime.Product.ProductDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            bindAddress = selected.Address;
        }

        var newBinding = new WebServerBindingOptions
        {
            Port = newPort,
            RemoteAccess = newRemoteAccess,
            BindAddress = bindAddress
        };

        if (newPort == current.Port
            && newBinding.RemoteAccess == current.RemoteAccess
            && string.Equals(newBinding.BindAddress, current.BindAddress, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            SaveWebServerSettings(newBinding);

            string message = $"Web server settings saved.\n\nPlease restart {IntercomRuntime.Product.ProductDisplayName} for the changes to take effect.";
            message += "\n\nLocal access: http://127.0.0.1:" + newPort;
            if (newRemoteAccess != WebServerRemoteAccess.Off)
            {
                string remotePreview = BuildRemoteUrlPreview(newPort, newRemoteAccess, interfaceCombo.SelectedItem as NetworkInterfaceOption);
                if (!string.IsNullOrWhiteSpace(remotePreview))
                {
                    message += "\n\nRemote access:\n" + remotePreview;
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

    private static void SelectInterfaceCombo(
        ComboBox combo,
        string? bindAddress,
        IReadOnlyList<NetworkInterfaceOption> interfaces)
    {
        if (interfaces.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(bindAddress))
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is NetworkInterfaceOption option
                    && option.Address == bindAddress)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        combo.SelectedIndex = 0;
    }

    private static string BuildRemoteUrlPreview(
        int port,
        WebServerRemoteAccess remoteAccess,
        NetworkInterfaceOption? selectedInterface)
    {
        return remoteAccess switch
        {
            WebServerRemoteAccess.Interface when selectedInterface != null =>
                $"http://{selectedInterface.Address}:{port}",
            WebServerRemoteAccess.All =>
                "All IPv4 addresses on this PC (use the IP of the shared network)",
            _ => string.Empty
        };
    }

    private void SaveWebServerSettings(WebServerBindingOptions binding)
    {
        string userPath = AppSettingsPaths.UserSettingsPath;
        Directory.CreateDirectory(AppSettingsPaths.UserSettingsDirectory);

        JsonNode? root;
        if (File.Exists(userPath))
        {
            string json = File.ReadAllText(userPath);
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

        binding.WriteTo(webServer, obj);

        var options = new JsonSerializerOptions { WriteIndented = true };
        string finalPath = userPath;
        string tempPath = finalPath + ".tmp";
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Best-effort cleanup of a stale temp file.
        }

        File.WriteAllText(tempPath, root.ToJsonString(options));
        File.Move(tempPath, finalPath, overwrite: true);
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
