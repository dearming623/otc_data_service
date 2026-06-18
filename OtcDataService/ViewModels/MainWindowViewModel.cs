using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public HomeViewModel Home { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty]
    private AppPage _currentPage = AppPage.Home;

    [ObservableProperty]
    private object? _currentContent;

    [ObservableProperty]
    private bool _isServiceEnabled;

    public bool IsOnHomePage => CurrentPage == AppPage.Home;
    public bool IsOnSettingsPage => CurrentPage == AppPage.Settings;
    public bool CanOpenSettings => !IsServiceEnabled;

    public string CurrentPageLabel => CurrentPage switch
    {
        AppPage.Home => "Home",
        AppPage.Settings => "Settings",
        _ => string.Empty
    };

    public string WindowTitle => AppInfo.WindowTitle;
    public string VersionLabel => AppInfo.VersionLabel;
    public string AppName => AppInfo.Name;

    public MainWindowViewModel()
    {
        Settings.RequestNavigateHome += (_, _) => NavigateHome();
        AppServices.ExportScheduler.RunningStateChanged += OnRunningStateChanged;
        IsServiceEnabled = AppServices.ExportScheduler.IsRunning;
        UpdateCurrentContent();
    }

    [RelayCommand]
    public void NavigateHome()
    {
        CurrentPage = AppPage.Home;
    }

    [RelayCommand]
    public void NavigateSettings()
    {
        if (IsServiceEnabled)
        {
            return;
        }

        Settings.Reload();
        CurrentPage = AppPage.Settings;
    }

    partial void OnCurrentPageChanged(AppPage value)
    {
        UpdateCurrentContent();
        OnPropertyChanged(nameof(IsOnHomePage));
        OnPropertyChanged(nameof(IsOnSettingsPage));
        OnPropertyChanged(nameof(CurrentPageLabel));
    }

    partial void OnIsServiceEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOpenSettings));
    }

    private void OnRunningStateChanged(object? sender, bool isRunning)
    {
        IsServiceEnabled = isRunning;
        if (isRunning && IsOnSettingsPage)
        {
            Settings.Reload();
            NavigateHome();
        }
    }

    private void UpdateCurrentContent()
    {
        CurrentContent = CurrentPage switch
        {
            AppPage.Home => Home,
            AppPage.Settings => Settings,
            _ => Home
        };
    }
}
