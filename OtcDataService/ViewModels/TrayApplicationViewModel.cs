using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Native;
using OtcDataService.Services;
using OtcDataService.Views;

namespace OtcDataService.ViewModels;

public partial class TrayApplicationViewModel : ViewModelBase
{
    private const string SetupRequiredMessage =
        "Please configure the database and export settings, then click Save to enable the service.";

    private NativeMenuItem? _enableMenuItem;
    private NativeMenuItem? _settingMenuItem;
    private TrayIcon? _trayIcon;
    private bool _isExiting;

    public MainWindowViewModel MainWindowViewModel { get; } = new();

    [ObservableProperty]
    private bool _isServiceEnabled;

    public void AttachTray(TrayIcon trayIcon, NativeMenuItem enableMenuItem, NativeMenuItem settingMenuItem)
    {
        _trayIcon = trayIcon;
        _enableMenuItem = enableMenuItem;
        _settingMenuItem = settingMenuItem;
        AppServices.ExportScheduler.RunningStateChanged += OnRunningStateChanged;
        IsServiceEnabled = AppServices.ExportScheduler.IsRunning;
        UpdateEnableMenuHeader();
        UpdateSettingMenuEnabled();
    }

    [RelayCommand]
    private async Task ToggleEnable()
    {
        if (IsServiceEnabled)
        {
            var confirmed = await EnterPasswordDialog.ShowAsync(
                "Disable Confirmation",
                "Enter the password to disable the export service.");
            if (!confirmed)
            {
                return;
            }

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
        if (IsServiceEnabled)
        {
            return;
        }

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

        var confirmed = await EnterPasswordDialog.ShowAsync();
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

        SingleInstanceGuard.Release();
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

    public void HideMainWindowToTray()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is MainWindow mainWindow)
        {
            mainWindow.Hide();
        }
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

    public void InitializeOnStartup()
    {
        if (!AppServices.Configuration.Current.HasCompletedSetup)
        {
            MainWindowViewModel.NavigateSettings();
            MainWindowViewModel.Settings.StatusMessage = SetupRequiredMessage;
            Win32MessageBox.ShowInfo(SetupRequiredMessage);
            return;
        }

        if (AppServices.ExportScheduler.TryStart(out var error))
        {
            return;
        }

        AppServices.Log.Error(error ?? "Failed to enable export scheduler on startup.");
        Win32MessageBox.ShowInfo(error ?? "Failed to enable export scheduler on startup.");
        MainWindowViewModel.NavigateSettings();
    }

    private void OnRunningStateChanged(object? sender, bool isRunning)
    {
        IsServiceEnabled = isRunning;
        UpdateEnableMenuHeader();
        UpdateSettingMenuEnabled();
    }

    private void UpdateEnableMenuHeader()
    {
        if (_enableMenuItem is not null)
        {
            _enableMenuItem.Header = IsServiceEnabled ? "Disable" : "Enable";
        }
    }

    private void UpdateSettingMenuEnabled()
    {
        if (_settingMenuItem is not null)
        {
            _settingMenuItem.IsEnabled = !IsServiceEnabled;
        }
    }
}
