namespace OtcDataService.Models;

public sealed class AppConfiguration
{
    public string OdbcDsn { get; set; } = "market2_64";
    public string OdbcUserId { get; set; } = "adm0";
    public string OdbcPassword { get; set; } = "systemcom";

    public const int MaxEntityIdLength = 50;

    public string EntityId { get; set; } = string.Empty;

    public int SalesLookbackDays { get; set; } = 7;
    public int DocumentIntervalDays { get; set; } = 1;
    public TimeOnly ExportTimeLocal { get; set; } = new(22, 0);
    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OtcDataService", "Exports");

    public bool FtpUploadEnabled { get; set; }
    public UploadProtocol UploadProtocol { get; set; } = UploadProtocol.Ftp;
    public UploadLogonType UploadLogonType { get; set; } = UploadLogonType.Normal;
    public string FtpHost { get; set; } = string.Empty;
    public int FtpPort { get; set; } = 21;
    public string FtpUserName { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string FtpRemotePath { get; set; } = "/";
    public FtpEncryptionMode FtpEncryptionMode { get; set; } = FtpEncryptionMode.ExplicitIfAvailable;

    public DateTime? LastExportUtc { get; set; }
    public bool IsEnabled { get; set; }
    public bool HasCompletedSetup { get; set; }
    public bool RunAtStartup { get; set; } = true;

    public string BuildConnectionString() =>
        $"Dsn={OdbcDsn};Uid={OdbcUserId};Pwd={OdbcPassword}";

    public static bool TryValidateEntityId(string? entityId, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            errorMessage = "Entity ID is required.";
            return false;
        }

        var value = entityId.Trim();
        if (value.Length > MaxEntityIdLength)
        {
            errorMessage = $"Entity ID must be {MaxEntityIdLength} characters or fewer.";
            return false;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            errorMessage = "Entity ID cannot contain spaces.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public string BuildCatalogExportFileName(DateTime timestamp) =>
        $"{EntityId.Trim()}_catalog_{timestamp:MMddyy}_{timestamp:HHmmss}.csv";

    public bool IsExportDue(DateTime nowLocal, DateTime? lastExportUtc)
    {
        var scheduledToday = nowLocal.Date + ExportTimeLocal.ToTimeSpan();

        if (lastExportUtc is null)
        {
            return nowLocal >= scheduledToday;
        }

        var lastLocal = lastExportUtc.Value.ToLocalTime();
        var daysSinceLastExport = (nowLocal.Date - lastLocal.Date).TotalDays;
        if (daysSinceLastExport < DocumentIntervalDays)
        {
            return false;
        }

        return nowLocal >= scheduledToday && lastLocal < scheduledToday;
    }
}
