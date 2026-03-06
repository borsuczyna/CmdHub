using System.Windows;
using CmdHub.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace CmdHub.Views;

public partial class EditCommandDialog : Window
{
    public CommandEntry? ResultEntry { get; private set; }

    public EditCommandDialog(CommandEntry? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            Title = "Edit Command";
            TxtName.Text = existing.Name;
            TxtCommand.Text = existing.Command;
            TxtWorkDir.Text = existing.WorkingDirectory;
            ChkAutoRestart.IsChecked = existing.AutoRestart;
            ChkRunOnStart.IsChecked = existing.RunOnStart;
            ChkUsePowerShell.IsChecked = existing.UsePowerShell;

            ChkRunEvery.IsChecked = existing.RunEveryEnabled;
            TxtRunEveryValue.Text = existing.RunEveryInterval > 0 ? existing.RunEveryInterval.ToString() : "5";
            SelectUnit(CmbRunEveryUnit, existing.RunEveryUnit, 1);

            ChkRestartEvery.IsChecked = existing.RestartEveryEnabled;
            TxtRestartEveryValue.Text = existing.RestartEveryInterval > 0 ? existing.RestartEveryInterval.ToString() : "5";
            SelectUnit(CmbRestartEveryUnit, existing.RestartEveryUnit, 1);
        }
        else
        {
            Title = "New Command";
            SelectUnit(CmbRunEveryUnit, "minutes", 1);
            SelectUnit(CmbRestartEveryUnit, "minutes", 1);
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select working directory",
            UseDescriptionForTitle = true,
            SelectedPath = TxtWorkDir.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtWorkDir.Text = dialog.SelectedPath;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        int runEveryInterval = 5;
        int restartEveryInterval = 5;

        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            WpfMessageBox.Show("Please enter a display name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtCommand.Text))
        {
            WpfMessageBox.Show("Please enter a command to execute.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCommand.Focus();
            return;
        }

        if (ChkRunEvery.IsChecked == true && !TryGetPositiveInterval(TxtRunEveryValue.Text, out runEveryInterval))
        {
            WpfMessageBox.Show("Run every interval must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtRunEveryValue.Focus();
            return;
        }

        if (ChkRestartEvery.IsChecked == true && !TryGetPositiveInterval(TxtRestartEveryValue.Text, out restartEveryInterval))
        {
            WpfMessageBox.Show("Restart every interval must be a positive number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtRestartEveryValue.Focus();
            return;
        }

        ResultEntry = new CommandEntry
        {
            Name = TxtName.Text.Trim(),
            Command = TxtCommand.Text.Trim(),
            WorkingDirectory = TxtWorkDir.Text.Trim(),
            AutoRestart = ChkAutoRestart.IsChecked == true,
            RunOnStart = ChkRunOnStart.IsChecked == true,
            UsePowerShell = ChkUsePowerShell.IsChecked == true,
            RunEveryEnabled = ChkRunEvery.IsChecked == true,
            RunEveryInterval = ChkRunEvery.IsChecked == true ? runEveryInterval : 5,
            RunEveryUnit = GetSelectedUnit(CmbRunEveryUnit),
            RestartEveryEnabled = ChkRestartEvery.IsChecked == true,
            RestartEveryInterval = ChkRestartEvery.IsChecked == true ? restartEveryInterval : 5,
            RestartEveryUnit = GetSelectedUnit(CmbRestartEveryUnit)
        };

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool TryGetPositiveInterval(string raw, out int value)
        => int.TryParse(raw.Trim(), out value) && value > 0;

    private static string GetSelectedUnit(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Content is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim().ToLowerInvariant();
        }

        return "minutes";
    }

    private static void SelectUnit(System.Windows.Controls.ComboBox comboBox, string? unit, int fallbackIndex)
    {
        string normalized = (unit ?? string.Empty).Trim().ToLowerInvariant();
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                item.Content is string text &&
                string.Equals(text.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = fallbackIndex;
    }
}
