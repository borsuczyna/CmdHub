using System;
using System.Diagnostics;
using System.IO;
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

    public bool UsePowerShell
    {
        get => Entry.UsePowerShell;
        set { Entry.UsePowerShell = value; OnPropertyChanged(); }
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
            var workDir = string.IsNullOrWhiteSpace(Entry.WorkingDirectory)
                ? Environment.CurrentDirectory
                : Entry.WorkingDirectory;

            if (!Directory.Exists(workDir))
            {
                Status = ProcessStatus.Crashed;
                AppendOutput($"[{DateTime.Now:HH:mm:ss}] [ERR] Invalid working directory: {workDir}\r\n");
                return;
            }

            var psi = BuildProcessStartInfo(Entry.Command, workDir, Entry.UsePowerShell);

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
        var process = _process;
        if (process == null) return;

        try
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (PlatformNotSupportedException)
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Already exited between checks.
                return;
            }

            // Give the process a moment to exit cleanly after kill.
            if (!process.WaitForExit(2500))
            {
                TryTaskKillTree(process.Id);
                process.WaitForExit(2500);
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"[{DateTime.Now:HH:mm:ss}] [ERR] Failed to kill process: {ex.Message}\r\n");
        }
    }

    private static void TryTaskKillTree(int pid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var killer = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            killer.Start();
            killer.WaitForExit(3000);
        }
        catch
        {
            // Swallow fallback kill errors; primary kill attempt already happened.
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            AppendOutput($"[{DateTime.Now:HH:mm:ss}] {e.Data}\r\n");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            AppendOutput($"[{DateTime.Now:HH:mm:ss}] [ERR] {e.Data}\r\n");
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

    private static ProcessStartInfo BuildProcessStartInfo(string rawCommand, string workingDirectory, bool usePowerShell)
    {
        var parsed = ParseCommand(rawCommand);
        if (string.IsNullOrWhiteSpace(parsed.fileName))
        {
            throw new InvalidOperationException("Empty command.");
        }

        if (usePowerShell && OperatingSystem.IsWindows())
        {
            return CreateStartInfo(
                "powershell.exe",
                $"-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"{EscapeForPowerShell(rawCommand)}\"",
                workingDirectory);
        }

        string fileName = parsed.fileName;
        string arguments = parsed.arguments;

        if (OperatingSystem.IsWindows())
        {
            string? resolved = ResolveWindowsCommand(fileName, workingDirectory);
            if (!string.IsNullOrEmpty(resolved))
            {
                fileName = resolved;
            }

            var extension = Path.GetExtension(fileName);
            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                string inner = QuoteForCmd(fileName);
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    inner += " " + arguments;
                }

                return CreateStartInfo("cmd.exe", $"/d /s /c \"{inner}\"", workingDirectory);
            }
        }

        return CreateStartInfo(fileName, arguments, workingDirectory);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string arguments, string workingDirectory)
        => new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            // Most modern CLIs (npm/vite/dotnet) emit UTF-8; force UTF-8 decode to avoid mojibake.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

    private static string EscapeForPowerShell(string command)
        => command.Replace("`", "``").Replace("\"", "`\"");

    private static string QuoteForCmd(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;

    private static string? ResolveWindowsCommand(string fileName, string workingDirectory)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        var extensions = string.IsNullOrWhiteSpace(pathExt)
            ? new[] { ".COM", ".EXE", ".BAT", ".CMD" }
            : pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool hasExtension = !string.IsNullOrEmpty(Path.GetExtension(fileName));
        bool hasDirectoryPart = Path.IsPathRooted(fileName) ||
            fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar);

        if (hasDirectoryPart)
        {
            var candidateBase = Path.IsPathRooted(fileName) ? fileName : Path.Combine(workingDirectory, fileName);
            if (hasExtension && File.Exists(candidateBase))
            {
                return candidateBase;
            }

            if (!hasExtension)
            {
                foreach (var ext in extensions)
                {
                    var candidate = candidateBase + ext.ToLowerInvariant();
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    candidate = candidateBase + ext.ToUpperInvariant();
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        if (hasExtension)
        {
            return fileName;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, fileName + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
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
