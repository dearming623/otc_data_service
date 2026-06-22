using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class DatabaseSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _odbcDsn = string.Empty;

    [ObservableProperty]
    private string _odbcUserId = string.Empty;

    [ObservableProperty]
    private string _odbcPassword = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    public DatabaseSettingsViewModel()
    {
        LoadFromConfiguration();
    }

    public void LoadFromConfiguration()
    {
        var config = AppServices.Configuration.Current;
        OdbcDsn = config.OdbcDsn;
        OdbcUserId = config.OdbcUserId;
        OdbcPassword = config.OdbcPassword;
        StatusMessage = null;
    }

    [RelayCommand]
    private void TestConnection()
    {
        var config = BuildDraftConfiguration();
        if (AppServices.Odbc.TestConnection(config, out var error))
        {
            StatusMessage = "Connection successful.";
        }
        else
        {
            StatusMessage = error ?? "Connection failed.";
        }
    }

    public bool SaveToConfiguration(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(OdbcDsn))
        {
            errorMessage = "DSN is required.";
            StatusMessage = errorMessage;
            return false;
        }

        AppServices.Configuration.Update(config =>
        {
            config.OdbcDsn = OdbcDsn.Trim();
            config.OdbcUserId = OdbcUserId.Trim();
            config.OdbcPassword = OdbcPassword;
        });

        StatusMessage = "Database settings saved.";
        errorMessage = null;
        AppServices.Log.Info("Database settings saved.");
        return true;
    }

    private AppConfiguration BuildDraftConfiguration()
    {
        var config = AppServices.Configuration.Current;
        return new AppConfiguration
        {
            OdbcDsn = OdbcDsn.Trim(),
            OdbcUserId = OdbcUserId.Trim(),
            OdbcPassword = OdbcPassword,
            SalesLookbackDays = config.SalesLookbackDays,
            DocumentIntervalDays = config.DocumentIntervalDays,
            OutputFolder = config.OutputFolder,
            FtpUploadEnabled = config.FtpUploadEnabled,
            UploadProtocol = config.UploadProtocol,
            UploadLogonType = config.UploadLogonType,
            FtpHost = config.FtpHost,
            FtpPort = config.FtpPort,
            FtpUserName = config.FtpUserName,
            FtpPassword = config.FtpPassword,
            FtpRemotePath = config.FtpRemotePath,
            FtpEncryptionMode = config.FtpEncryptionMode,
            LastExportUtc = config.LastExportUtc,
            IsEnabled = config.IsEnabled
        };
    }
}
