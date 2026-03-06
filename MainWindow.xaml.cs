using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using CmdHub.Services;
using CmdHub.ViewModels;

namespace CmdHub;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private NotifyIcon? _notifyIcon;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new ConfigService());
        DataContext = _viewModel;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "CmdHub — Multi-Console Manager",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Show CmdHub", null, (_, _) => ShowMainWindow());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _notifyIcon.BalloonTipTitle = "CmdHub";
        _notifyIcon.BalloonTipText = "CmdHub is running in the background.";
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        CleanupForExit();
        System.Windows.Application.Current.Shutdown();
    }

    public void CleanupForExit()
    {
        _isExiting = true;
        _viewModel.Cleanup();
        _notifyIcon?.Dispose();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting) return;

        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
        _notifyIcon?.ShowBalloonTip(
            1500,
            "CmdHub",
            "CmdHub is minimized to the tray. Right-click the tray icon to exit.",
            ToolTipIcon.Info);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }
}
