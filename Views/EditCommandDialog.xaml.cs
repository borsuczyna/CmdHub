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
        }
        else
        {
            Title = "New Command";
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

        ResultEntry = new CommandEntry
        {
            Name = TxtName.Text.Trim(),
            Command = TxtCommand.Text.Trim(),
            WorkingDirectory = TxtWorkDir.Text.Trim(),
            AutoRestart = ChkAutoRestart.IsChecked == true,
            RunOnStart = ChkRunOnStart.IsChecked == true
        };

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
