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

    [ObservableProperty]
    private DateTimeOffset? _startDate;

    [ObservableProperty]
    private DateTimeOffset? _endDate;

    [ObservableProperty]
    private bool _isDateRangeReadOnly;

    public ManualViewModel()
    {
        RefreshDateRange();
    }

    public void RefreshDateRange()
    {
        var config = AppServices.Configuration.Current;
        if (config.SalesLookbackDays > 0)
        {
            var today = DateTime.Today;
            StartDate = today.AddDays(-config.SalesLookbackDays);
            EndDate = today;
            IsDateRangeReadOnly = true;
        }
        else
        {
            StartDate = null;
            EndDate = null;
            IsDateRangeReadOnly = false;
        }

        ExportNowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanExportNow))]
    private async Task ExportNow()
    {
        var config = AppServices.Configuration.Current;

        if (config.SalesLookbackDays <= 0)
        {
            StatusMessage = "Sales lookback days must be greater than zero.";
            return;
        }

        if (StartDate is null || EndDate is null)
        {
            StatusMessage = "Start date and end date are required.";
            return;
        }

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
            var startDate = DateOnly.FromDateTime(StartDate.Value.DateTime);
            var endDate = DateOnly.FromDateTime(EndDate.Value.DateTime).AddDays(1);
            var success = await AppServices.ExportData.ExportAsync(
                startDate: startDate,
                endDate: endDate,
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

    private bool CanExportNow()
    {
        var config = AppServices.Configuration.Current;
        return !IsExporting
            && config.SalesLookbackDays > 0
            && StartDate is not null
            && EndDate is not null;
    }

    partial void OnIsExportingChanged(bool value)
    {
        ExportNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnStartDateChanged(DateTimeOffset? value)
    {
        ExportNowCommand.NotifyCanExecuteChanged();
    }

    partial void OnEndDateChanged(DateTimeOffset? value)
    {
        ExportNowCommand.NotifyCanExecuteChanged();
    }

    public void ClearStatus()
    {
        StatusMessage = null;
    }
}
