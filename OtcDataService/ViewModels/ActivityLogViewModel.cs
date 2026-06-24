using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class ActivityLogViewModel : ViewModelBase
{
    public ObservableCollection<LogEntry> Logs => AppServices.Log.Entries;

    [RelayCommand]
    private void ClearLogs()
    {
        AppServices.Log.Clear();
        AppServices.Log.Info("Log cleared.");
    }
}
