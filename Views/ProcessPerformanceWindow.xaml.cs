using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CmdHub.Models;
using CmdHub.ViewModels;
using MediaColor = System.Windows.Media.Color;

namespace CmdHub.Views;

public partial class ProcessPerformanceWindow : Window
{
    public CommandViewModel CommandVm { get; }

    private readonly DispatcherTimer _refreshTimer;
    private TimeSpan? _lastTotalProcessorTime;
    private DateTime? _lastSampleUtc;

    public ProcessPerformanceWindow(CommandViewModel vm)
    {
        InitializeComponent();
        CommandVm = vm;
        Title = $"{vm.Name} - Performance";

        vm.PropertyChanged += OnVmPropertyChanged;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        RefreshMetrics();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshMetrics();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandViewModel.Status))
        {
            Dispatcher.InvokeAsync(RefreshMetrics);
        }
    }

    private void RefreshMetrics()
    {
        if (!CommandVm.TryCaptureProcessSnapshot(out ProcessPerformanceSnapshot snapshot))
        {
            SetStoppedState();
            return;
        }

        TxtStatusBadge.Text = "Running";
        TxtStatusBadge.Foreground = CreateBrush(0x4C, 0xAF, 0x50);

        TxtProcessName.Text = snapshot.ProcessName;
        TxtPid.Text = snapshot.ProcessId.ToString();
        TxtWorkingSet.Text = FormatBytes(snapshot.WorkingSetBytes);
        TxtPrivateMemory.Text = FormatBytes(snapshot.PrivateMemoryBytes);
        TxtThreadsHandles.Text = snapshot.HandleCount.HasValue
            ? $"{snapshot.ThreadCount} / {snapshot.HandleCount.Value}"
            : $"{snapshot.ThreadCount} / n/a";

        if (snapshot.StartTimeLocal.HasValue)
        {
            TimeSpan uptime = DateTime.Now - snapshot.StartTimeLocal.Value;
            if (uptime < TimeSpan.Zero)
            {
                uptime = TimeSpan.Zero;
            }

            TxtUptime.Text = FormatDuration(uptime);
        }
        else
        {
            TxtUptime.Text = "n/a";
        }

        TxtUpdated.Text = DateTime.Now.ToString("HH:mm:ss");
        TxtCpu.Text = FormatCpu(snapshot);
        TxtHint.Text = "Sampling every 1 second.";
    }

    private string FormatCpu(ProcessPerformanceSnapshot snapshot)
    {
        if (!_lastTotalProcessorTime.HasValue || !_lastSampleUtc.HasValue)
        {
            _lastTotalProcessorTime = snapshot.TotalProcessorTime;
            _lastSampleUtc = snapshot.SampledAtUtc;
            return "warming up...";
        }

        double cpuMs = (snapshot.TotalProcessorTime - _lastTotalProcessorTime.Value).TotalMilliseconds;
        double elapsedMs = (snapshot.SampledAtUtc - _lastSampleUtc.Value).TotalMilliseconds;

        _lastTotalProcessorTime = snapshot.TotalProcessorTime;
        _lastSampleUtc = snapshot.SampledAtUtc;

        if (elapsedMs <= 0)
        {
            return "0.0%";
        }

        double cpuPercent = cpuMs / (elapsedMs * Environment.ProcessorCount) * 100.0;
        cpuPercent = Math.Clamp(cpuPercent, 0.0, 100.0);
        return $"{cpuPercent:0.0}%";
    }

    private void SetStoppedState()
    {
        TxtStatusBadge.Text = "Stopped";
        TxtStatusBadge.Foreground = CreateBrush(0xAA, 0xAA, 0xAA);

        TxtProcessName.Text = "-";
        TxtPid.Text = "-";
        TxtCpu.Text = "-";
        TxtWorkingSet.Text = "-";
        TxtPrivateMemory.Text = "-";
        TxtThreadsHandles.Text = "-";
        TxtUptime.Text = "-";
        TxtUpdated.Text = DateTime.Now.ToString("HH:mm:ss");
        TxtHint.Text = "Start the command to view live process metrics.";

        _lastTotalProcessorTime = null;
        _lastSampleUtc = null;
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        }

        return $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private static string FormatBytes(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = kilobyte * 1024d;
        const double gigabyte = megabyte * 1024d;

        if (bytes >= gigabyte)
        {
            return $"{bytes / gigabyte:0.00} GB";
        }

        if (bytes >= megabyte)
        {
            return $"{bytes / megabyte:0.00} MB";
        }

        if (bytes >= kilobyte)
        {
            return $"{bytes / kilobyte:0.00} KB";
        }

        return $"{bytes} B";
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(MediaColor.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        CommandVm.PropertyChanged -= OnVmPropertyChanged;
    }
}
