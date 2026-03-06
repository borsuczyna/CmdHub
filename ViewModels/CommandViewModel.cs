using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CmdHub.Models;
using WpfApp = System.Windows.Application;

namespace CmdHub.ViewModels;

public enum ProcessStatus
{
    Stopped,
    Running,
    Crashed
}

public class CommandViewModel : BaseViewModel, IDisposable
{
    private Process? _process;
    private ProcessStatus _status = ProcessStatus.Stopped;
    private bool _manualStop;
    private bool _restarting;
    private bool _disposed;
    private CancellationTokenSource? _restartCts;
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _outputLock = new();
    private const int MaxOutputLength = 500_000;

    public CommandEntry Entry { get; }

    public string Name
    {
        get => Entry.Name;
        set { Entry.Name = value; OnPropertyChanged(); }
    }

    public string Command
    {
        get => Entry.Command;
        set { Entry.Command = value; OnPropertyChanged(); }
    }

    public string WorkingDirectory
    {
        get => Entry.WorkingDirectory;
        set { Entry.WorkingDirectory = value; OnPropertyChanged(); }
    }

    public bool AutoRestart
    {
        get => Entry.AutoRestart;
        set { Entry.AutoRestart = value; OnPropertyChanged(); }
    }

    public bool RunOnStart
    {
        get => Entry.RunOnStart;
        set { Entry.RunOnStart = value; OnPropertyChanged(); }
    }

    public ProcessStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
            }
        }
    }

    public string StatusText => Status switch
    {
        ProcessStatus.Running => "Running",
        ProcessStatus.Crashed => "Crashed",
        _ => "Stopped"
    };

    private static readonly System.Windows.Media.SolidColorBrush RunningBrush =
        new(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly System.Windows.Media.SolidColorBrush CrashedBrush =
        new(System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36));
    private static readonly System.Windows.Media.SolidColorBrush StoppedBrush =
        new(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E));

    public System.Windows.Media.SolidColorBrush StatusBrush => Status switch
    {
        ProcessStatus.Running => RunningBrush,
        ProcessStatus.Crashed => CrashedBrush,
        _ => StoppedBrush
    };

    public bool CanStart => Status != ProcessStatus.Running;
    public bool CanStop => Status == ProcessStatus.Running;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }

    public event Action<string>? OutputReceived;

    public CommandViewModel(CommandEntry entry)
    {
        Entry = entry;
        StartCommand = new RelayCommand(() => Start(), () => CanStart);
        StopCommand = new RelayCommand(() => Stop(), () => CanStop);
        RestartCommand = new RelayCommand(() => Restart());
    }

    public void Start()
    {
        if (Status == ProcessStatus.Running) return;
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        _restartCts = new CancellationTokenSource();
        _manualStop = false;
        _restarting = false;
        StartInternal();
    }

    private void StartInternal()
    {
        try
        {
            var parsed = ParseCommand(Entry.Command);
            if (string.IsNullOrEmpty(parsed.fileName))
            {
                AppendOutput("[CmdHub] Error: Empty command.\r\n");
                return;
            }

            var workDir = string.IsNullOrWhiteSpace(Entry.WorkingDirectory)
                ? Environment.CurrentDirectory
                : Entry.WorkingDirectory;

            var psi = new ProcessStartInfo
            {
                FileName = parsed.fileName,
                Arguments = parsed.arguments,
                WorkingDirectory = workDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnProcessExited;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Status = ProcessStatus.Running;
            AppendOutput($"[{DateTime.Now:HH:mm:ss}] Process started (PID: {_process.Id})\r\n");
        }
        catch (Exception ex)
        {
            Status = ProcessStatus.Crashed;
            AppendOutput($"[{DateTime.Now:HH:mm:ss}] Failed to start: {ex.Message}\r\n");
        }
    }

    public void Stop()
    {
        _manualStop = true;
        _restarting = false;
        _restartCts?.Cancel();
        KillProcess();
    }

    public void Restart()
    {
        _restarting = true;
        _manualStop = false;
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        _restartCts = new CancellationTokenSource();

        if (Status == ProcessStatus.Running)
        {
            KillProcess();
            // OnProcessExited will handle StartInternal
        }
        else
        {
            _restarting = false;
            StartInternal();
        }
    }

    private void KillProcess()
    {
        if (_process == null) return;
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            AppendOutput(e.Data + "\r\n");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            AppendOutput("[ERR] " + e.Data + "\r\n");
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        int exitCode = 0;
        try { exitCode = _process?.ExitCode ?? 0; } catch { }

        AppendOutput($"[{DateTime.Now:HH:mm:ss}] Process exited with code {exitCode}\r\n");

        bool wasRestarting = _restarting;
        _restarting = false;

        SafeDispatch(() =>
        {
            if (_manualStop)
            {
                Status = ProcessStatus.Stopped;
                return;
            }

            if (wasRestarting)
            {
                Status = ProcessStatus.Stopped;
                StartInternal();
                return;
            }

            // Natural exit or crash
            Status = exitCode == 0 ? ProcessStatus.Stopped : ProcessStatus.Crashed;

            if (Entry.AutoRestart)
            {
                var cts = _restartCts;
                if (cts != null && !cts.IsCancellationRequested)
                {
                    Task.Run(async () =>
                    {
                        AppendOutput($"[{DateTime.Now:HH:mm:ss}] Auto-restarting in 3 seconds...\r\n");
                        try
                        {
                            await Task.Delay(3000, cts.Token);
                        }
                        catch (OperationCanceledException) { return; }

                        SafeDispatch(StartInternal);
                    });
                }
            }
        });
    }

    private static void SafeDispatch(Action action)
    {
        try
        {
            var app = WpfApp.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(action);
        }
        catch
        {
            // Silently ignore dispatcher errors during app shutdown
        }
    }

    private void AppendOutput(string text)
    {
        lock (_outputLock)
        {
            _outputBuffer.Append(text);
            if (_outputBuffer.Length > MaxOutputLength)
            {
                int removeCount = MaxOutputLength / 5;
                _outputBuffer.Remove(0, removeCount);
            }
        }
        OutputReceived?.Invoke(text);
    }

    public string GetFullOutput()
    {
        lock (_outputLock)
        {
            return _outputBuffer.ToString();
        }
    }

    public void ClearOutput()
    {
        lock (_outputLock)
        {
            _outputBuffer.Clear();
        }
    }

    private static (string fileName, string arguments) ParseCommand(string command)
    {
        command = command.Trim();
        if (string.IsNullOrEmpty(command))
            return (string.Empty, string.Empty);

        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 0) return (command, string.Empty);
            string fileName = command[1..end];
            string args = end + 2 < command.Length ? command[(end + 2)..].Trim() : string.Empty;
            return (fileName, args);
        }
        else
        {
            int spaceIdx = command.IndexOf(' ');
            if (spaceIdx < 0) return (command, string.Empty);
            return (command[..spaceIdx], command[(spaceIdx + 1)..].Trim());
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        KillProcess();
        _process?.Dispose();
    }
}
