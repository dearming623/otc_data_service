using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OtcDataService.Models;
using OtcDataService.Services;

namespace OtcDataService.ViewModels;

public partial class ExportSettingsViewModel : ViewModelBase
{
    private const int DefaultFtpPort = 21;
    private const int DefaultSftpPort = 22;
    private const int DefaultImplicitFtpPort = 990;

    [ObservableProperty]
    private int _salesLookbackDays;

    [ObservableProperty]
    private int _documentIntervalDays;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _ftpUploadEnabled;

    [ObservableProperty]
    private UploadProtocol _uploadProtocol = UploadProtocol.Ftp;

    [ObservableProperty]
    private UploadLogonType _uploadLogonType = UploadLogonType.Normal;

    [ObservableProperty]
    private UploadSelectOption<UploadProtocol>? _selectedProtocolOption;

    [ObservableProperty]
    private UploadSelectOption<UploadLogonType>? _selectedLogonTypeOption;

    [ObservableProperty]
    private FtpEncryptionMode _ftpEncryptionMode = FtpEncryptionMode.ExplicitIfAvailable;

    [ObservableProperty]
    private UploadSelectOption<FtpEncryptionMode>? _selectedEncryptionOption;

    [ObservableProperty]
    private string _ftpHost = string.Empty;

    [ObservableProperty]
    private int _ftpPort = DefaultFtpPort;

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

    public IReadOnlyList<UploadSelectOption<UploadProtocol>> ProtocolOptions { get; } =
        new UploadSelectOption<UploadProtocol>[]
        {
            new(UploadProtocol.Ftp, "FTP - File Transfer Protocol"),
            new(UploadProtocol.Sftp, "SFTP - SSH File Transfer Protocol")
        };

    public IReadOnlyList<UploadSelectOption<UploadLogonType>> LogonTypeOptions { get; } =
        new UploadSelectOption<UploadLogonType>[]
        {
            new(UploadLogonType.Anonymous, "Anonymous"),
            new(UploadLogonType.Normal, "Normal")
        };

    public IReadOnlyList<UploadSelectOption<FtpEncryptionMode>> EncryptionOptions { get; } =
        new UploadSelectOption<FtpEncryptionMode>[]
        { 
            new(FtpEncryptionMode.ExplicitIfAvailable, "Use explicit FTP over TLS if available"),
            new(FtpEncryptionMode.ExplicitRequired, "Require explicit FTP over TLS"),
            new(FtpEncryptionMode.ImplicitRequired, "Require implicit FTP over TLS"),
            new(FtpEncryptionMode.None, "Only use plain FTP (insecure)"),
        };

    public bool IsFtpProtocol => UploadProtocol == UploadProtocol.Ftp;

    public bool IsNormalLogon => UploadLogonType == UploadLogonType.Normal;

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
        UploadProtocol = config.UploadProtocol;
        UploadLogonType = config.UploadLogonType;
        SelectedProtocolOption = FindProtocolOption(config.UploadProtocol);
        SelectedLogonTypeOption = FindLogonTypeOption(config.UploadLogonType);
        FtpEncryptionMode = config.FtpEncryptionMode;
        SelectedEncryptionOption = FindEncryptionOption(config.FtpEncryptionMode);
        FtpHost = config.FtpHost;
        FtpPort = config.FtpPort;
        FtpUserName = config.FtpUserName;
        FtpPassword = config.FtpPassword;
        FtpRemotePath = config.FtpRemotePath;
        RunAtStartup = config.RunAtStartup;
        StatusMessage = null;
    }

    partial void OnSelectedProtocolOptionChanged(UploadSelectOption<UploadProtocol>? value)
    {
        if (value is null)
        {
            return;
        }

        if (UploadProtocol != value.Value)
        {
            UploadProtocol = value.Value;
            FtpPort = value.Value == UploadProtocol.Sftp ? DefaultSftpPort : DefaultFtpPort;
            OnPropertyChanged(nameof(IsFtpProtocol));
        }
    }

    partial void OnUploadProtocolChanged(UploadProtocol value)
    {
        SelectedProtocolOption = FindProtocolOption(value);
        OnPropertyChanged(nameof(IsFtpProtocol));
    }

    partial void OnSelectedLogonTypeOptionChanged(UploadSelectOption<UploadLogonType>? value)
    {
        if (value is null)
        {
            return;
        }

        if (UploadLogonType != value.Value)
        {
            UploadLogonType = value.Value;
            OnPropertyChanged(nameof(IsNormalLogon));
        }
    }

    partial void OnUploadLogonTypeChanged(UploadLogonType value)
    {
        SelectedLogonTypeOption = FindLogonTypeOption(value);
        OnPropertyChanged(nameof(IsNormalLogon));
    }

    partial void OnSelectedEncryptionOptionChanged(UploadSelectOption<FtpEncryptionMode>? value)
    {
        if (value is null)
        {
            return;
        }

        if (FtpEncryptionMode != value.Value)
        {
            FtpEncryptionMode = value.Value;
            ApplyDefaultPortForEncryptionMode(value.Value);
        }
    }

    partial void OnFtpEncryptionModeChanged(FtpEncryptionMode value)
    {
        SelectedEncryptionOption = FindEncryptionOption(value);
    }

    private void ApplyDefaultPortForEncryptionMode(FtpEncryptionMode mode)
    {
        FtpPort = mode == FtpEncryptionMode.ImplicitRequired
            ? DefaultImplicitFtpPort
            : DefaultFtpPort;
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
        if (!RemoteUploadService.ValidateSettings(config, out var validationError))
        {
            StatusMessage = validationError;
            return;
        }

        if (!config.FtpUploadEnabled)
        {
            StatusMessage = "Enable remote upload to test the connection.";
            return;
        }

        var protocolLabel = RemoteUploadService.GetProtocolLabel(config.UploadProtocol);

        try
        {
            await AppServices.RemoteUpload.TestConnectionAsync(config);
            StatusMessage = $"{protocolLabel} connection successful.";
            AppServices.Log.Info($"{protocolLabel} connection test successful.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"{protocolLabel} connection failed: {ex.Message}";
            AppServices.Log.Error($"{protocolLabel} connection test failed: {ex.Message}");
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
        if (!RemoteUploadService.ValidateSettings(draft, out errorMessage))
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
            config.UploadProtocol = UploadProtocol;
            config.UploadLogonType = UploadLogonType;
            config.FtpHost = FtpHost.Trim();
            config.FtpPort = FtpPort;
            config.FtpUserName = FtpUserName.Trim();
            config.FtpPassword = FtpPassword;
            config.FtpRemotePath = string.IsNullOrWhiteSpace(FtpRemotePath) ? "/" : FtpRemotePath.Trim();
            config.FtpEncryptionMode = FtpEncryptionMode;
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
            UploadProtocol = UploadProtocol,
            UploadLogonType = UploadLogonType,
            FtpHost = FtpHost.Trim(),
            FtpPort = FtpPort,
            FtpUserName = FtpUserName.Trim(),
            FtpPassword = FtpPassword,
            FtpRemotePath = string.IsNullOrWhiteSpace(FtpRemotePath) ? "/" : FtpRemotePath.Trim(),
            FtpEncryptionMode = FtpEncryptionMode
        };

    private UploadSelectOption<UploadProtocol>? FindProtocolOption(UploadProtocol protocol) =>
        ProtocolOptions.FirstOrDefault(option => option.Value == protocol);

    private UploadSelectOption<UploadLogonType>? FindLogonTypeOption(UploadLogonType logonType) =>
        LogonTypeOptions.FirstOrDefault(option => option.Value == logonType);

    private UploadSelectOption<FtpEncryptionMode>? FindEncryptionOption(FtpEncryptionMode mode) =>
        EncryptionOptions.FirstOrDefault(option => option.Value == mode);
}
