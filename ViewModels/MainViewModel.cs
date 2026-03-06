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
    private readonly ApiHostService _apiHostService;
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

    public string ApiStatusText => ApiEnabled ? $"API: On ({_config.ApiPort})" : "API: Off";
    public string ApiHintText => ApiEnabled
        ? $"Click to disable API. Base URL: http://localhost:{_config.ApiPort}/api"
        : $"Click to enable API at http://localhost:{_config.ApiPort}/api";

    public ICommand NewCommandCommand { get; }
    public ICommand ToggleApiCommand { get; }
    public ICommand OpenConsoleCommand { get; }
    public ICommand OpenPerformanceCommand { get; }
    public ICommand EditCommandCommand { get; }
    public ICommand DeleteCommandCommand { get; }

    public MainViewModel(ConfigService configService)
    {
        _configService = configService;
        _config = configService.Load();
        _apiHostService = new ApiHostService(GetCommandsSnapshotForApi, _config.ApiPort);

        foreach (var entry in _config.Commands)
            AddCommandViewModel(new CommandViewModel(entry));

        NewCommandCommand = new RelayCommand(OpenNewCommandDialog);
        ToggleApiCommand = new RelayCommand(ToggleApiEnabled);
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
            _apiHostService.Stop();
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
            _apiHostService.Start();
            ApiEnabled = true;
            SaveConfig();
        }
        catch (Exception ex)
        {
            ApiEnabled = false;
            SaveConfig();
            WpfMessageBox.Show(
                $"Could not start API host on port {_config.ApiPort}:\n{ex.Message}",
                "API Host Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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
        _apiHostService.Stop();

        foreach (var cmd in Commands)
        {
            cmd.Stop();
            cmd.Dispose();
        }
    }
}
