using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public DatabaseSettingsViewModel Database { get; } = new();
    public ExportSettingsViewModel Export { get; } = new();

    [ObservableProperty]
    private SettingsSection _selectedSection = SettingsSection.Database;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSettingsLocked;

    public bool AreSettingsEditable => !IsSettingsLocked;

    public bool IsDatabaseSelected => SelectedSection == SettingsSection.Database;
    public bool IsExportSelected => SelectedSection == SettingsSection.Export;

    public ViewModelBase SelectedSectionContent => SelectedSection switch
    {
        SettingsSection.Database => Database,
        SettingsSection.Export => Export,
        _ => Database
    };

    public string SelectedSectionTitle => SelectedSection switch
    {
        SettingsSection.Database => "Database Settings",
        SettingsSection.Export => "Export Settings",
        _ => "Settings"
    };

    public SettingsViewModel()
    {
        AppServices.ExportScheduler.RunningStateChanged += OnRunningStateChanged;
        IsSettingsLocked = AppServices.ExportScheduler.IsRunning;
    }

    partial void OnIsSettingsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(AreSettingsEditable));
    }

    private void OnRunningStateChanged(object? sender, bool isRunning)
    {
        IsSettingsLocked = isRunning;
        if (isRunning)
        {
            Reload();
        }
    }

    partial void OnSelectedSectionChanged(SettingsSection value)
    {
        OnPropertyChanged(nameof(IsDatabaseSelected));
        OnPropertyChanged(nameof(IsExportSelected));
        OnPropertyChanged(nameof(SelectedSectionContent));
        OnPropertyChanged(nameof(SelectedSectionTitle));
    }

    public event EventHandler? RequestNavigateHome;

    [RelayCommand]
    private void SelectDatabase() => SelectedSection = SettingsSection.Database;

    [RelayCommand]
    private void SelectExport() => SelectedSection = SettingsSection.Export;

    [RelayCommand]
    private void Save()
    {
        if (IsSettingsLocked)
        {
            return;
        }

        if (!Database.SaveToConfiguration(out var databaseError))
        {
            StatusMessage = databaseError;
            SelectedSection = SettingsSection.Database;
            return;
        }

        if (!Export.SaveToConfiguration(out var exportError))
        {
            StatusMessage = exportError;
            SelectedSection = SettingsSection.Export;
            return;
        }

        StatusMessage = "All settings saved.";
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsSettingsLocked)
        {
            return;
        }

        Database.LoadFromConfiguration();
        Export.LoadFromConfiguration();
        StatusMessage = "Changes discarded.";
    }

    [RelayCommand]
    private void Back()
    {
        RequestNavigateHome?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        Database.LoadFromConfiguration();
        Export.LoadFromConfiguration();
        StatusMessage = null;
    }
}
