using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class ManualViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand(CanExecute = nameof(CanExportNow))]
    private async Task ExportNow()
    {
        var config = AppServices.Configuration.Current;

        if (string.IsNullOrWhiteSpace(config.OutputFolder))
        {
            StatusMessage = "Output folder is required.";
            return;
        }

        if (!AppConfiguration.TryValidateEntityId(config.EntityId, out var entityIdError))
        {
            StatusMessage = entityIdError;
            return;
        }

        IsExporting = true;
        StatusMessage = "Exporting...";

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var success = await AppServices.ExportData.ExportAsync(
                startDate: today,
                endDate: today.AddDays(1),
                updateLastExportUtc: false);

            StatusMessage = success
                ? "Export completed successfully."
                : "Export failed. See Activity Log for details.";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private bool CanExportNow() => !IsExporting;

    partial void OnIsExportingChanged(bool value)
    {
        ExportNowCommand.NotifyCanExecuteChanged();
    }

    public void ClearStatus()
    {
        StatusMessage = null;
    }
}
