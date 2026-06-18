using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class ExportSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _salesLookbackDays;

    [ObservableProperty]
    private int _documentIntervalDays;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _ftpUploadEnabled;

    [ObservableProperty]
    private string _ftpHost = string.Empty;

    [ObservableProperty]
    private int _ftpPort = 21;

    [ObservableProperty]
    private string _ftpUserName = string.Empty;

    [ObservableProperty]
    private string _ftpPassword = string.Empty;

    [ObservableProperty]
    private string _ftpRemotePath = "/";

    [ObservableProperty]
    private bool _runAtStartup = true;

    [ObservableProperty]
    private string? _statusMessage;

    public ExportSettingsViewModel()
    {
        LoadFromConfiguration();
    }

    public void LoadFromConfiguration()
    {
        var config = AppServices.Configuration.Current;
        SalesLookbackDays = config.SalesLookbackDays;
        DocumentIntervalDays = config.DocumentIntervalDays;
        OutputFolder = config.OutputFolder;
        FtpUploadEnabled = config.FtpUploadEnabled;
        FtpHost = config.FtpHost;
        FtpPort = config.FtpPort;
        FtpUserName = config.FtpUserName;
        FtpPassword = config.FtpPassword;
        FtpRemotePath = config.FtpRemotePath;
        RunAtStartup = config.RunAtStartup;
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task BrowseOutputFolderAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider is null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            OutputFolder = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task TestFtpConnectionAsync()
    {
        var config = BuildDraftConfiguration();
        if (!FtpUploadService.ValidateSettings(config, out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        if (!config.FtpUploadEnabled)
        {
            StatusMessage = "Enable FTP upload to test the connection.";
            return;
        }

        try
        {
            await AppServices.FtpUpload.TestConnectionAsync(config);
            StatusMessage = "FTP connection successful.";
            AppServices.Log.Info("FTP connection test successful.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"FTP connection failed: {ex.Message}";
            AppServices.Log.Error($"FTP connection test failed: {ex.Message}");
        }
    }

    public bool SaveToConfiguration(out string? errorMessage)
    {
        if (SalesLookbackDays <= 0)
        {
            errorMessage = "Sales lookback days must be greater than zero.";
            StatusMessage = errorMessage;
            return false;
        }

        if (DocumentIntervalDays <= 0)
        {
            errorMessage = "Document interval days must be greater than zero.";
            StatusMessage = errorMessage;
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            errorMessage = "Output folder is required.";
            StatusMessage = errorMessage;
            return false;
        }

        var draft = BuildDraftConfiguration();
        if (!FtpUploadService.ValidateSettings(draft, out errorMessage))
        {
            StatusMessage = errorMessage;
            return false;
        }

        AppServices.Configuration.Update(config =>
        {
            config.SalesLookbackDays = SalesLookbackDays;
            config.DocumentIntervalDays = DocumentIntervalDays;
            config.OutputFolder = OutputFolder.Trim();
            config.FtpUploadEnabled = FtpUploadEnabled;
            config.FtpHost = FtpHost.Trim();
            config.FtpPort = FtpPort;
            config.FtpUserName = FtpUserName.Trim();
            config.FtpPassword = FtpPassword;
            config.FtpRemotePath = string.IsNullOrWhiteSpace(FtpRemotePath) ? "/" : FtpRemotePath.Trim();
            config.RunAtStartup = RunAtStartup;
        });

        if (!AppServices.Startup.Sync(RunAtStartup, out errorMessage))
        {
            StatusMessage = errorMessage;
            return false;
        }

        StatusMessage = "Export settings saved.";
        errorMessage = null;
        AppServices.Log.Info("Export settings saved.");
        return true;
    }

    private AppConfiguration BuildDraftConfiguration() =>
        new()
        {
            SalesLookbackDays = SalesLookbackDays,
            DocumentIntervalDays = DocumentIntervalDays,
            OutputFolder = OutputFolder.Trim(),
            FtpUploadEnabled = FtpUploadEnabled,
            FtpHost = FtpHost.Trim(),
            FtpPort = FtpPort,
            FtpUserName = FtpUserName.Trim(),
            FtpPassword = FtpPassword,
            FtpRemotePath = string.IsNullOrWhiteSpace(FtpRemotePath) ? "/" : FtpRemotePath.Trim()
        };
}
