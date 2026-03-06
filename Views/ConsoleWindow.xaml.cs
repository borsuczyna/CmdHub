using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CmdHub.ViewModels;
using WpfBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfClipboard = System.Windows.Clipboard;

namespace CmdHub.Views;

public partial class ConsoleWindow : Window
{
    public CommandViewModel CommandVm { get; }

    private const int MaxDisplayLines = 500;
    private int _lineCount;
    private string _pendingTailText = string.Empty;
    private Paragraph? _pendingTailParagraph;
    private WpfBrush _lastNonEmptyLineBrush = DefaultOutputBrush;
    private bool _showTimestamps;

    private static readonly SolidColorBrush DefaultOutputBrush = CreateBrush(0xCC, 0xCC, 0xCC);
    private static readonly SolidColorBrush ErrorBrush = CreateBrush(0xFF, 0x6B, 0x6B);
    private static readonly SolidColorBrush InfoBrush = CreateBrush(0x66, 0xC0, 0xFF);
    private static readonly SolidColorBrush WarningBrush = CreateBrush(0xFF, 0xC1, 0x07);
    private static readonly SolidColorBrush DebugBrush = CreateBrush(0x9E, 0x9E, 0x9E);
    private static readonly IReadOnlyDictionary<int, SolidColorBrush> AnsiForegroundMap =
        new Dictionary<int, SolidColorBrush>
        {
            [30] = CreateBrush(0x00, 0x00, 0x00),
            [31] = CreateBrush(0xCD, 0x31, 0x31),
            [32] = CreateBrush(0x0D, 0xBC, 0x79),
            [33] = CreateBrush(0xE5, 0xE5, 0x10),
            [34] = CreateBrush(0x24, 0x73, 0xC2),
            [35] = CreateBrush(0xBC, 0x3F, 0xBC),
            [36] = CreateBrush(0x11, 0xA8, 0xCD),
            [37] = CreateBrush(0xE5, 0xE5, 0xE5),
            [90] = CreateBrush(0x66, 0x66, 0x66),
            [91] = CreateBrush(0xF1, 0x4C, 0x4C),
            [92] = CreateBrush(0x23, 0xD1, 0x8B),
            [93] = CreateBrush(0xF5, 0xF5, 0x43),
            [94] = CreateBrush(0x3B, 0x8E, 0xEA),
            [95] = CreateBrush(0xD6, 0x70, 0xD6),
            [96] = CreateBrush(0x29, 0xB8, 0xDB),
            [97] = CreateBrush(0xFF, 0xFF, 0xFF)
        };

    public ConsoleWindow(CommandViewModel vm)
    {
        InitializeComponent();
        CommandVm = vm;
        Title = $"{vm.Name} — Console";

        ResetOutputRenderState();
        UpdateTimestampButtonText();

        // Show existing output
        var existing = vm.GetFullOutput();
        if (!string.IsNullOrEmpty(existing))
            AppendChunk(GetTailLines(existing, MaxDisplayLines));

        // Subscribe to future output
        vm.OutputReceived += OnOutputReceived;
        vm.PropertyChanged += OnVmPropertyChanged;

        UpdateStatusDisplay();
    }

    private void OnOutputReceived(string text)
    {
        Dispatcher.InvokeAsync(() => AppendChunk(text));
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
        BtnCtrlC.IsEnabled = CommandVm.CanStop;
    }

    private void AppendChunk(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // Replace previously rendered partial tail so the next chunk can continue the same line.
        if (_pendingTailParagraph != null)
        {
            OutputBox.Document.Blocks.Remove(_pendingTailParagraph);
            _pendingTailParagraph = null;
        }

        string combined = _pendingTailText + normalized;
        string[] parts = combined.Split('\n');
        int completedLines = parts.Length - 1;

        for (int i = 0; i < completedLines; i++)
        {
            AddLine(parts[i]);
        }

        _pendingTailText = combined.EndsWith('\n') ? string.Empty : parts[^1];
        if (!string.IsNullOrEmpty(_pendingTailText))
        {
            _pendingTailParagraph = CreateLineParagraph(_pendingTailText);
            OutputBox.Document.Blocks.Add(_pendingTailParagraph);
        }

        OutputBox.ScrollToEnd();
    }

    private void AddLine(string line)
    {
        OutputBox.Document.Blocks.Add(CreateLineParagraph(line));
        _lineCount++;

        while (_lineCount > MaxDisplayLines)
        {
            var first = OutputBox.Document.Blocks.FirstBlock;
            if (first == null || first == _pendingTailParagraph)
            {
                break;
            }

            OutputBox.Document.Blocks.Remove(first);
            _lineCount--;
        }
    }

    private Paragraph CreateLineParagraph(string line)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 18
        };

        var fallbackBrush = GetFallbackBrushForLine(line, _lastNonEmptyLineBrush);
        if (!string.IsNullOrWhiteSpace(line))
        {
            _lastNonEmptyLineBrush = fallbackBrush;
        }

        string displayLine = _showTimestamps ? line : StripLeadingTimestamp(line);

        BuildAnsiRuns(paragraph, displayLine, fallbackBrush);

        if (paragraph.Inlines.FirstInline == null)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        return paragraph;
    }

    private static void BuildAnsiRuns(Paragraph paragraph, string text, WpfBrush fallbackBrush)
    {
        WpfBrush currentBrush = fallbackBrush;
        int index = 0;

        while (index < text.Length)
        {
            if (text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
            {
                int end = text.IndexOf('m', index + 2);
                if (end > index)
                {
                    string payload = text.Substring(index + 2, end - (index + 2));
                    currentBrush = ApplyAnsiSgr(payload, fallbackBrush, currentBrush);
                    index = end + 1;
                    continue;
                }
            }

            int nextEscape = text.IndexOf('\u001b', index);
            int segmentEnd = nextEscape >= 0 ? nextEscape : text.Length;
            if (segmentEnd > index)
            {
                var run = new Run(text.Substring(index, segmentEnd - index))
                {
                    Foreground = currentBrush
                };
                paragraph.Inlines.Add(run);
            }

            index = segmentEnd;
        }
    }

    private static WpfBrush ApplyAnsiSgr(string payload, WpfBrush fallbackBrush, WpfBrush currentBrush)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return fallbackBrush;
        }

        WpfBrush brush = currentBrush;
        var parts = payload.Split(';');

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out int code))
            {
                continue;
            }

            if (code == 0 || code == 39)
            {
                brush = fallbackBrush;
                continue;
            }

            if (AnsiForegroundMap.TryGetValue(code, out var mapped))
            {
                brush = mapped;
            }
        }

        return brush;
    }

    private static WpfBrush GetFallbackBrushForLine(string line, WpfBrush previousBrush)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return previousBrush;
        }

        if (char.IsWhiteSpace(line[0]) && previousBrush != DefaultOutputBrush)
        {
            return previousBrush;
        }

        string trimmed = StripLeadingTimestamp(line.TrimStart());

        if (trimmed.StartsWith("[ERR]", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("crit:", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorBrush;
        }

        if (trimmed.StartsWith("warn:", StringComparison.OrdinalIgnoreCase))
        {
            return WarningBrush;
        }

        if (trimmed.StartsWith("info:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("[CmdHub]", StringComparison.Ordinal) ||
            (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains("Process", StringComparison.OrdinalIgnoreCase)))
        {
            return InfoBrush;
        }

        if (trimmed.StartsWith("dbug:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("trce:", StringComparison.OrdinalIgnoreCase))
        {
            return DebugBrush;
        }

        return DefaultOutputBrush;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(MediaColor.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static bool HasLeadingTimestamp(string line)
    {
        if (line.Length < 10)
        {
            return false;
        }

        // Match [HH:mm:ss] prefix.
        return line[0] == '[' &&
               char.IsDigit(line[1]) &&
               char.IsDigit(line[2]) &&
               line[3] == ':' &&
               char.IsDigit(line[4]) &&
               char.IsDigit(line[5]) &&
               line[6] == ':' &&
               char.IsDigit(line[7]) &&
               char.IsDigit(line[8]) &&
               line[9] == ']';
    }

    private static string StripLeadingTimestamp(string line)
    {
        if (!HasLeadingTimestamp(line))
        {
            return line;
        }

        if (line.Length > 10 && line[10] == ' ')
        {
            return line[11..];
        }

        return line[10..];
    }

    private void UpdateTimestampButtonText()
    {
        BtnTimestamp.Content = _showTimestamps ? "Timestamps: On" : "Timestamps: Off";
    }

    private void ResetOutputRenderState()
    {
        OutputBox.Document.Blocks.Clear();
        _lineCount = 0;
        _pendingTailText = string.Empty;
        _pendingTailParagraph = null;
        _lastNonEmptyLineBrush = DefaultOutputBrush;
    }

    private void RerenderOutput()
    {
        var snapshot = CommandVm.GetFullOutput();
        ResetOutputRenderState();

        if (!string.IsNullOrEmpty(snapshot))
        {
            AppendChunk(GetTailLines(snapshot, MaxDisplayLines));
        }
    }

    private static string GetTailLines(string text, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0)
        {
            return string.Empty;
        }

        int seenNewlines = 0;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            seenNewlines++;
            if (seenNewlines > maxLines)
            {
                return text[(i + 1)..];
            }
        }

        return text;
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
        => CommandVm.Start();

    private void BtnStop_Click(object sender, RoutedEventArgs e)
        => CommandVm.Stop();

    private void BtnCtrlC_Click(object sender, RoutedEventArgs e)
    {
        if (CommandVm.SendCtrlC())
        {
            TxtStatusBar.Text = $"{CommandVm.Name}  —  Sent Ctrl+C";
        }
        else
        {
            TxtStatusBar.Text = $"{CommandVm.Name}  —  Ctrl+C not sent";
        }
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
        => CommandVm.Restart();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ResetOutputRenderState();
        CommandVm.ClearOutput();
    }

    private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WpfClipboard.SetText(CommandVm.GetFullOutput());
            TxtStatusBar.Text = $"{CommandVm.Name}  —  Copied to clipboard";
        }
        catch
        {
            TxtStatusBar.Text = $"{CommandVm.Name}  —  Copy failed";
        }
    }

    private void BtnTimestamp_Click(object sender, RoutedEventArgs e)
    {
        _showTimestamps = !_showTimestamps;
        UpdateTimestampButtonText();
        RerenderOutput();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        CommandVm.OutputReceived -= OnOutputReceived;
        CommandVm.PropertyChanged -= OnVmPropertyChanged;
    }
}
