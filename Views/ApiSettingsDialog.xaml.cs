using System.Windows;
using CmdHub.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace CmdHub.Views;

public partial class ApiSettingsDialog : Window
{
    public int ApiPort { get; private set; }
    public int ControlPanelPort { get; private set; }
    public string ControlPanelPassword { get; private set; } = string.Empty;

    public ApiSettingsDialog(int apiPort, int controlPanelPort, string controlPanelPassword)
    {
        InitializeComponent();

        TxtApiPort.Text = apiPort.ToString();
        TxtControlPanelPort.Text = controlPanelPort.ToString();
        TxtPassword.Text = controlPanelPassword;
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        TxtPassword.Text = ConfigService.GenerateRandomPassword();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParsePort(TxtApiPort.Text, out int apiPort))
        {
            WpfMessageBox.Show("API port must be between 1 and 65535.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtApiPort.Focus();
            return;
        }

        if (!TryParsePort(TxtControlPanelPort.Text, out int controlPanelPort))
        {
            WpfMessageBox.Show("Control panel port must be between 1 and 65535.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtControlPanelPort.Focus();
            return;
        }

        if (apiPort == controlPanelPort)
        {
            WpfMessageBox.Show("API port and control panel port must be different.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtControlPanelPort.Focus();
            return;
        }

        var password = TxtPassword.Text.Trim();
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            WpfMessageBox.Show("Password must be at least 8 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPassword.Focus();
            return;
        }

        ApiPort = apiPort;
        ControlPanelPort = controlPanelPort;
        ControlPanelPassword = password;

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool TryParsePort(string raw, out int port)
        => int.TryParse(raw.Trim(), out port) && port is >= 1 and <= 65535;
}
