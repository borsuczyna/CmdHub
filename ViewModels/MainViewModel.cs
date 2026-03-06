using System;
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

    public ICommand NewCommandCommand { get; }
    public ICommand OpenConsoleCommand { get; }
    public ICommand EditCommandCommand { get; }
    public ICommand DeleteCommandCommand { get; }

    public MainViewModel(ConfigService configService)
    {
        _configService = configService;
        _config = configService.Load();

        foreach (var entry in _config.Commands)
            AddCommandViewModel(new CommandViewModel(entry));

        NewCommandCommand = new RelayCommand(OpenNewCommandDialog);
        OpenConsoleCommand = new RelayCommand(p => OpenConsole(p as CommandViewModel));
        EditCommandCommand = new RelayCommand(p => OpenEditDialog(p as CommandViewModel));
        DeleteCommandCommand = new RelayCommand(p => DeleteCommand(p as CommandViewModel));

        // Start commands with RunOnStart
        foreach (var cmd in Commands.Where(c => c.RunOnStart))
            cmd.Start();
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

    private void OpenNewCommandDialog()
    {
        var dialog = new EditCommandDialog();
        if (dialog.ShowDialog() == true && dialog.ResultEntry != null)
        {
            var vm = new CommandViewModel(dialog.ResultEntry);
            AddCommandViewModel(vm);
            _config.Commands = Commands.Select(c => c.Entry).ToList();
            _configService.Save(_config);

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

            _config.Commands = Commands.Select(c => c.Entry).ToList();
            _configService.Save(_config);
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

        _config.Commands = Commands.Select(c => c.Entry).ToList();
        _configService.Save(_config);

        OnPropertyChanged(nameof(RunningCount));
    }

    public void Cleanup()
    {
        foreach (var cmd in Commands)
        {
            cmd.Stop();
            cmd.Dispose();
        }
    }
}
