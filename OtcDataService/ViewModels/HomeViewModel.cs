using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;

namespace OtcDataService.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public ActivityLogViewModel ActivityLog { get; } = new();
    public ManualViewModel Manual { get; } = new();

    [ObservableProperty]
    private HomeSection _selectedSection = HomeSection.ActivityLog;

    public bool IsActivityLogSelected => SelectedSection == HomeSection.ActivityLog;
    public bool IsManualSelected => SelectedSection == HomeSection.Manual;

    public ViewModelBase SelectedSectionContent => SelectedSection switch
    {
        HomeSection.ActivityLog => ActivityLog,
        HomeSection.Manual => Manual,
        _ => ActivityLog
    };

    public string SelectedSectionTitle => SelectedSection switch
    {
        HomeSection.ActivityLog => "Activity Log",
        HomeSection.Manual => "Manual",
        _ => "Home"
    };

    partial void OnSelectedSectionChanged(HomeSection value)
    {
        if (value == HomeSection.ActivityLog)
        {
            Manual.ClearStatus();
        }

        OnPropertyChanged(nameof(IsActivityLogSelected));
        OnPropertyChanged(nameof(IsManualSelected));
        OnPropertyChanged(nameof(SelectedSectionContent));
        OnPropertyChanged(nameof(SelectedSectionTitle));
    }

    [RelayCommand]
    private void SelectActivityLog() => SelectedSection = HomeSection.ActivityLog;

    [RelayCommand]
    private void SelectManual() => SelectedSection = HomeSection.Manual;
}
