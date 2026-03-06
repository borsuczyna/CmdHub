using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CmdHub.ViewModels;
using MediaColor = System.Windows.Media.Color;
using WpfClipboard = System.Windows.Clipboard;

namespace CmdHub.Views;

public partial class ConsoleWindow : Window
{
    public CommandViewModel CommandVm { get; }

    private const int MaxDisplayLines = 5000;
    private int _lineCount;

    public ConsoleWindow(CommandViewModel vm)
    {
        InitializeComponent();
        CommandVm = vm;
        Title = $"{vm.Name} — Console";

        // Show existing output
        var existing = vm.GetFullOutput();
        if (!string.IsNullOrEmpty(existing))
            AppendText(existing);

        // Subscribe to future output
        vm.OutputReceived += OnOutputReceived;
        vm.PropertyChanged += OnVmPropertyChanged;

        UpdateStatusDisplay();
    }

    private void OnOutputReceived(string text)
    {
        Dispatcher.InvokeAsync(() => AppendText(text));
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandViewModel.Status))
            Dispatcher.InvokeAsync(UpdateStatusDisplay);
    }

    private void UpdateStatusDisplay()
    {
        var brush = CommandVm.StatusBrush;
        StatusDot.Fill = brush;
        TxtStatus.Text = CommandVm.StatusText;
        TxtStatus.Foreground = brush;
        TxtStatusBar.Text = $"{CommandVm.Name}  —  {CommandVm.StatusText}";
        BtnStart.IsEnabled = CommandVm.CanStart;
        BtnStop.IsEnabled = CommandVm.CanStop;
    }

    private void AppendText(string text)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            LineHeight = 18
        };

        // Color error lines differently
        if (text.StartsWith("[ERR]"))
            paragraph.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x6B, 0x6B));
        else if (text.StartsWith("[CmdHub]") || text.StartsWith("[") && text.Contains("Process"))
            paragraph.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x85, 0x85, 0xFF));

        OutputBox.Document.Blocks.Add(paragraph);
        _lineCount++;

        // Trim old lines to keep memory usage in check
        if (_lineCount > MaxDisplayLines)
        {
            var blocks = OutputBox.Document.Blocks;
            if (blocks.FirstBlock != null)
            {
                blocks.Remove(blocks.FirstBlock);
                _lineCount--;
            }
        }

        OutputBox.ScrollToEnd();
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
        => CommandVm.Start();

    private void BtnStop_Click(object sender, RoutedEventArgs e)
        => CommandVm.Stop();

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
        => CommandVm.Restart();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Document.Blocks.Clear();
        _lineCount = 0;
        CommandVm.ClearOutput();
    }

    private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WpfClipboard.SetText(CommandVm.GetFullOutput());
        }
        catch { }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        CommandVm.OutputReceived -= OnOutputReceived;
        CommandVm.PropertyChanged -= OnVmPropertyChanged;
    }
}
