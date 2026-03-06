using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CmdHub.Models;
using CmdHub.Services;
using CmdHub.Views;
using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace CmdHub.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private ApiHostService? _apiHostService;
    private ControlPanelHostService? _controlPanelHostService;
    private CommandViewModel? _selectedCommand;

    public ObservableCollection<CommandViewModel> Commands { get; } = new();

    public CommandViewModel? SelectedCommand
    {
        get => _selectedCommand;
        set => SetProperty(ref _selectedCommand, value);
    }

    public string RunningCount
    {
        get
        {
            int count = Commands.Count(c => c.Status == ProcessStatus.Running);
            return count == 1 ? "1 process running" : $"{count} processes running";
        }
    }

    public bool ApiEnabled
    {
        get => _config.ApiEnabled;
        private set
        {
            if (_config.ApiEnabled == value)
            {
                return;
            }

            _config.ApiEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ApiStatusText));
            OnPropertyChanged(nameof(ApiHintText));
        }
    }

    public string ApiStatusText => ApiEnabled
        ? $"Remote: On (API {_config.ApiPort} | Panel {_config.ControlPanelPort})"
        : "Remote: Off";
    public string ApiHintText => ApiEnabled
        ? $"Remote API enabled. API: http://<this-machine-ip>:{_config.ApiPort}/api | Panel: http://<this-machine-ip>:{_config.ControlPanelPort}/"
        : $"Click to enable remote API and control panel.";

    public ICommand NewCommandCommand { get; }
    public ICommand ToggleApiCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenConsoleCommand { get; }
    public ICommand OpenPerformanceCommand { get; }
    public ICommand EditCommandCommand { get; }
    public ICommand DeleteCommandCommand { get; }

    public MainViewModel(ConfigService configService)
    {
        _configService = configService;
        _config = configService.Load();

        foreach (var entry in _config.Commands)
            AddCommandViewModel(new CommandViewModel(entry));

        NewCommandCommand = new RelayCommand(OpenNewCommandDialog);
        ToggleApiCommand = new RelayCommand(ToggleApiEnabled);
        OpenSettingsCommand = new RelayCommand(OpenSettingsDialog);
        OpenConsoleCommand = new RelayCommand(p => OpenConsole(p as CommandViewModel));
        OpenPerformanceCommand = new RelayCommand(p => OpenPerformance(p as CommandViewModel));
        EditCommandCommand = new RelayCommand(p => OpenEditDialog(p as CommandViewModel));
        DeleteCommandCommand = new RelayCommand(p => DeleteCommand(p as CommandViewModel));

        if (_config.ApiEnabled)
        {
            TryStartApiHost();
        }

        // Start commands with RunOnStart
        foreach (var cmd in Commands.Where(c => c.RunOnStart))
            cmd.Start();
    }

    private void EnsureHostServices()
    {
        if (_apiHostService == null || _apiHostService.Port != _config.ApiPort)
        {
            _apiHostService?.Stop();
            _apiHostService = new ApiHostService(
                GetCommandsSnapshotForApi,
                () => _config.ControlPanelPassword,
                CreateCommandFromRemote,
                UpdateCommandFromRemote,
                DeleteCommandFromRemote,
                StartCommandFromRemote,
                StopCommandFromRemote,
                RestartCommandFromRemote,
                CtrlCCommandFromRemote,
                ClearLogsFromRemote,
                _config.ApiPort);
        }

        if (_controlPanelHostService == null || _controlPanelHostService.Port != _config.ControlPanelPort)
        {
            _controlPanelHostService?.Stop();
            _controlPanelHostService = new ControlPanelHostService(_config.ControlPanelPort, () => _config.ApiPort);
        }
    }

    private IReadOnlyList<CommandViewModel> GetCommandsSnapshotForApi()
    {
        var app = WpfApp.Current;
        if (app == null)
        {
            return Array.Empty<CommandViewModel>();
        }

        return app.Dispatcher.Invoke(() => Commands.ToList());
    }

    private void ToggleApiEnabled()
    {
        if (ApiEnabled)
        {
            _apiHostService?.Stop();
            _controlPanelHostService?.Stop();
            ApiEnabled = false;
            SaveConfig();
            return;
        }

        TryStartApiHost();
    }

    private void TryStartApiHost()
    {
        try
        {
            EnsureHostServices();
            _apiHostService?.Start();
            _controlPanelHostService?.Start();
            ApiEnabled = true;
            SaveConfig();

            if ((_apiHostService != null && !_apiHostService.IsLanAccessible) ||
                (_controlPanelHostService != null && !_controlPanelHostService.IsLanAccessible))
            {
                string apiAcl = HttpListenerPrefixHelper.BuildUrlAclCommand(_config.ApiPort);
                string panelAcl = HttpListenerPrefixHelper.BuildUrlAclCommand(_config.ControlPanelPort);

                WpfMessageBox.Show(
                    "Remote hosts started in localhost-only mode because Windows denied URL ACL access for LAN bindings.\n\n" +
                    "To allow access from other devices, run PowerShell as Administrator and execute:\n\n" +
                    apiAcl + "\n" +
                    panelAcl,
                    "Remote Host Permissions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ApiEnabled = false;
            SaveConfig();
            WpfMessageBox.Show(
                $"Could not start remote hosts.\nAPI port: {_config.ApiPort}\nControl panel port: {_config.ControlPanelPort}\n\n{ex.Message}",
                "Remote Host Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenSettingsDialog()
    {
        var dialog = new ApiSettingsDialog(_config.ApiPort, _config.ControlPanelPort, _config.ControlPanelPassword);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        bool requiresRestart = _config.ApiPort != dialog.ApiPort || _config.ControlPanelPort != dialog.ControlPanelPort;

        _config.ApiPort = dialog.ApiPort;
        _config.ControlPanelPort = dialog.ControlPanelPort;
        _config.ControlPanelPassword = dialog.ControlPanelPassword;
        SaveConfig();

        if (!ApiEnabled)
        {
            OnPropertyChanged(nameof(ApiStatusText));
            OnPropertyChanged(nameof(ApiHintText));
            return;
        }

        if (requiresRestart)
        {
            _apiHostService?.Stop();
            _controlPanelHostService?.Stop();
            TryStartApiHost();
        }
    }

    private OperationResult CreateCommandFromRemote(RemoteCommandUpsert payload)
        => RunOnUiThread(() =>
        {
            var validation = ValidateRemotePayload(payload);
            if (!validation.Success)
            {
                return validation;
            }

            var entry = CreateEntry(payload);
            var vm = new CommandViewModel(entry);
            AddCommandViewModel(vm);
            SaveConfig();

            if (entry.RunOnStart)
            {
                vm.Start();
            }

            return OperationResult.Ok(entry.Id);
        });

    private OperationResult UpdateCommandFromRemote(Guid id, RemoteCommandUpsert payload)
        => RunOnUiThread(() =>
        {
            var validation = ValidateRemotePayload(payload);
            if (!validation.Success)
            {
                return validation;
            }

            var vm = Commands.FirstOrDefault(c => c.Entry.Id == id);
            if (vm == null)
            {
                return OperationResult.Fail("Process not found.");
            }

            ApplyPayloadToVm(vm, payload);
            SaveConfig();
            return OperationResult.Ok(id);
        });

    private OperationResult DeleteCommandFromRemote(Guid id)
        => RunOnUiThread(() =>
        {
            var vm = Commands.FirstOrDefault(c => c.Entry.Id == id);
            if (vm == null)
            {
                return OperationResult.Fail("Process not found.");
            }

            foreach (var w in WpfApp.Current.Windows
                .OfType<ConsoleWindow>()
                .Where(w => w.CommandVm == vm)
                .ToList())
            {
                w.Close();
            }

            vm.Stop();
            vm.Dispose();
            Commands.Remove(vm);
            SaveConfig();
            OnPropertyChanged(nameof(RunningCount));
            return OperationResult.Ok(id);
        });

    private OperationResult StartCommandFromRemote(Guid id)
        => RunOnUiThread(() => ExecuteOnVm(id, vm => { vm.Start(); return OperationResult.Ok(id); }));

    private OperationResult StopCommandFromRemote(Guid id)
        => RunOnUiThread(() => ExecuteOnVm(id, vm => { vm.Stop(); return OperationResult.Ok(id); }));

    private OperationResult RestartCommandFromRemote(Guid id)
        => RunOnUiThread(() => ExecuteOnVm(id, vm => { vm.Restart(); return OperationResult.Ok(id); }));

    private OperationResult CtrlCCommandFromRemote(Guid id)
        => RunOnUiThread(() => ExecuteOnVm(id, vm => vm.SendCtrlC() ? OperationResult.Ok(id) : OperationResult.Fail("Ctrl+C not delivered.")));

    private OperationResult ClearLogsFromRemote(Guid id)
        => RunOnUiThread(() => ExecuteOnVm(id, vm => { vm.ClearOutput(); return OperationResult.Ok(id); }));

    private OperationResult ExecuteOnVm(Guid id, Func<CommandViewModel, OperationResult> operation)
    {
        var vm = Commands.FirstOrDefault(c => c.Entry.Id == id);
        if (vm == null)
        {
            return OperationResult.Fail("Process not found.");
        }

        return operation(vm);
    }

    private static CommandEntry CreateEntry(RemoteCommandUpsert payload)
        => new()
        {
            Name = payload.Name.Trim(),
            Command = payload.Command.Trim(),
            WorkingDirectory = payload.WorkingDirectory.Trim(),
            AutoRestart = payload.AutoRestart,
            RunOnStart = payload.RunOnStart,
            UsePowerShell = payload.UsePowerShell,
            RunEveryEnabled = payload.RunEveryEnabled,
            RunEveryInterval = payload.RunEveryInterval,
            RunEveryUnit = NormalizeUnit(payload.RunEveryUnit),
            RestartEveryEnabled = payload.RestartEveryEnabled,
            RestartEveryInterval = payload.RestartEveryInterval,
            RestartEveryUnit = NormalizeUnit(payload.RestartEveryUnit)
        };

    private static void ApplyPayloadToVm(CommandViewModel vm, RemoteCommandUpsert payload)
    {
        vm.Name = payload.Name.Trim();
        vm.Command = payload.Command.Trim();
        vm.WorkingDirectory = payload.WorkingDirectory.Trim();
        vm.AutoRestart = payload.AutoRestart;
        vm.RunOnStart = payload.RunOnStart;
        vm.UsePowerShell = payload.UsePowerShell;
        vm.RunEveryEnabled = payload.RunEveryEnabled;
        vm.RunEveryInterval = payload.RunEveryInterval;
        vm.RunEveryUnit = NormalizeUnit(payload.RunEveryUnit);
        vm.RestartEveryEnabled = payload.RestartEveryEnabled;
        vm.RestartEveryInterval = payload.RestartEveryInterval;
        vm.RestartEveryUnit = NormalizeUnit(payload.RestartEveryUnit);
    }

    private static OperationResult ValidateRemotePayload(RemoteCommandUpsert payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return OperationResult.Fail("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.Command))
        {
            return OperationResult.Fail("Command is required.");
        }

        if (payload.RunEveryInterval <= 0 || payload.RestartEveryInterval <= 0)
        {
            return OperationResult.Fail("Intervals must be greater than zero.");
        }

        if (!IsSupportedUnit(payload.RunEveryUnit) || !IsSupportedUnit(payload.RestartEveryUnit))
        {
            return OperationResult.Fail("Units must be seconds, minutes, or hours.");
        }

        return OperationResult.Ok();
    }

    private static bool IsSupportedUnit(string? unit)
    {
        string normalized = NormalizeUnit(unit);
        return normalized is "seconds" or "minutes" or "hours";
    }

    private static string NormalizeUnit(string? unit)
    {
        string normalized = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "second" => "seconds",
            "minute" => "minutes",
            "hour" => "hours",
            _ => normalized
        };
    }

    private static T RunOnUiThread<T>(Func<T> action)
    {
        var app = WpfApp.Current;
        if (app == null)
        {
            return action();
        }

        if (app.Dispatcher.CheckAccess())
        {
            return action();
        }

        return app.Dispatcher.Invoke(action);
    }

    private void AddCommandViewModel(CommandViewModel vm)
    {
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandViewModel.Status))
                OnPropertyChanged(nameof(RunningCount));
        };
        Commands.Add(vm);
    }

    private void SaveConfig()
    {
        _config.Commands = Commands.Select(c => c.Entry).ToList();
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Could not save configuration:\n{ex.Message}",
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenNewCommandDialog()
    {
        var dialog = new EditCommandDialog();
        if (dialog.ShowDialog() == true && dialog.ResultEntry != null)
        {
            var vm = new CommandViewModel(dialog.ResultEntry);
            AddCommandViewModel(vm);
            SaveConfig();

            if (dialog.ResultEntry.RunOnStart)
                vm.Start();
        }
    }

    private void OpenEditDialog(CommandViewModel? vm)
    {
        if (vm == null) return;
        var dialog = new EditCommandDialog(vm.Entry);
        if (dialog.ShowDialog() == true && dialog.ResultEntry != null)
        {
            vm.Name = dialog.ResultEntry.Name;
            vm.Command = dialog.ResultEntry.Command;
            vm.WorkingDirectory = dialog.ResultEntry.WorkingDirectory;
            vm.AutoRestart = dialog.ResultEntry.AutoRestart;
            vm.RunOnStart = dialog.ResultEntry.RunOnStart;
            vm.UsePowerShell = dialog.ResultEntry.UsePowerShell;
            vm.RunEveryEnabled = dialog.ResultEntry.RunEveryEnabled;
            vm.RunEveryInterval = dialog.ResultEntry.RunEveryInterval;
            vm.RunEveryUnit = dialog.ResultEntry.RunEveryUnit;
            vm.RestartEveryEnabled = dialog.ResultEntry.RestartEveryEnabled;
            vm.RestartEveryInterval = dialog.ResultEntry.RestartEveryInterval;
            vm.RestartEveryUnit = dialog.ResultEntry.RestartEveryUnit;
            SaveConfig();
        }
    }

    private void OpenConsole(CommandViewModel? vm)
    {
        if (vm == null) return;
        var existing = WpfApp.Current.Windows
            .OfType<ConsoleWindow>()
            .FirstOrDefault(w => w.CommandVm == vm);

        if (existing != null)
        {
            existing.Activate();
            existing.WindowState = WindowState.Normal;
        }
        else
        {
            var consoleWindow = new ConsoleWindow(vm);
            consoleWindow.Show();
        }
    }

    private void OpenPerformance(CommandViewModel? vm)
    {
        if (vm == null) return;

        var existing = WpfApp.Current.Windows
            .OfType<ProcessPerformanceWindow>()
            .FirstOrDefault(w => w.CommandVm == vm);

        if (existing != null)
        {
            existing.Activate();
            existing.WindowState = WindowState.Normal;
        }
        else
        {
            var performanceWindow = new ProcessPerformanceWindow(vm);
            performanceWindow.Show();
        }
    }

    private void DeleteCommand(CommandViewModel? vm)
    {
        if (vm == null) return;

        var result = WpfMessageBox.Show(
            $"Delete \"{vm.Name}\"? This will stop the process if running.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // Close any open console window for this vm
        foreach (var w in WpfApp.Current.Windows
            .OfType<ConsoleWindow>()
            .Where(w => w.CommandVm == vm)
            .ToList())
        {
            w.Close();
        }

        vm.Stop();
        vm.Dispose();
        Commands.Remove(vm);
        SaveConfig();

        OnPropertyChanged(nameof(RunningCount));
    }

    public void Cleanup()
    {
        _apiHostService?.Stop();
        _controlPanelHostService?.Stop();

        foreach (var cmd in Commands)
        {
            cmd.Stop();
            cmd.Dispose();
        }
    }
}
