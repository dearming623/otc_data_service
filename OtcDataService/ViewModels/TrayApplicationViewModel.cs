using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Services;
using OtcDataService.Views;

namespace OtcDataService.ViewModels;

public partial class TrayApplicationViewModel : ViewModelBase
{
    private NativeMenuItem? _enableMenuItem;
    private TrayIcon? _trayIcon;
    private bool _isExiting;

    public MainWindowViewModel MainWindowViewModel { get; } = new();

    [ObservableProperty]
    private bool _isServiceEnabled;

    public void AttachTray(TrayIcon trayIcon, NativeMenuItem enableMenuItem)
    {
        _trayIcon = trayIcon;
        _enableMenuItem = enableMenuItem;
        AppServices.ExportScheduler.RunningStateChanged += OnRunningStateChanged;
        IsServiceEnabled = AppServices.ExportScheduler.IsRunning;
        UpdateEnableMenuHeader();
    }

    [RelayCommand]
    private void ToggleEnable()
    {
        if (IsServiceEnabled)
        {
            AppServices.ExportScheduler.Stop();
            return;
        }

        if (!AppServices.ExportScheduler.TryStart(out var error))
        {
            AppServices.Log.Error(error ?? "Failed to enable export scheduler.");
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ShowMainWindow();
        MainWindowViewModel.NavigateSettings();
    }

    [RelayCommand]
    private async Task Exit()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var confirmed = await ExitPasswordDialog.ShowAsync();
        if (!confirmed)
        {
            return;
        }

        PerformShutdown();
    }

    private void PerformShutdown()
    {
        _isExiting = true;
        AppServices.ExportScheduler.Stop();
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { IsVisible: true } mainWindow)
        {
            mainWindow.Close();
        }

        Environment.Exit(0);
    }

    public void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is not MainWindow mainWindow)
        {
            mainWindow = new MainWindow
            {
                DataContext = MainWindowViewModel
            };
            desktop.MainWindow = mainWindow;
            ConfigureMainWindow(mainWindow);
        }

        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    public void ConfigureMainWindow(MainWindow mainWindow)
    {
        mainWindow.DataContext = MainWindowViewModel;
        mainWindow.Closing += (_, e) =>
        {
            if (_isExiting)
            {
                return;
            }

            e.Cancel = true;
            mainWindow.Hide();
        };
    }

    private void OnRunningStateChanged(object? sender, bool isRunning)
    {
        IsServiceEnabled = isRunning;
        UpdateEnableMenuHeader();
    }

    private void UpdateEnableMenuHeader()
    {
        if (_enableMenuItem is not null)
        {
            _enableMenuItem.Header = IsServiceEnabled ? "Disable" : "Enable";
        }
    }
}
