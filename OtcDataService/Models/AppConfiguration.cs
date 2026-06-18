namespace OtcDataService.Models;

public sealed class AppConfiguration
{
    public string OdbcDsn { get; set; } = "market2_64";
    public string OdbcUserId { get; set; } = "adm0";
    public string OdbcPassword { get; set; } = "systemcom";

    public int SalesLookbackDays { get; set; } = 7;
    public int DocumentIntervalDays { get; set; } = 1;
    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OtcDataService", "Exports");

    public bool FtpUploadEnabled { get; set; }
    public string FtpHost { get; set; } = string.Empty;
    public int FtpPort { get; set; } = 21;
    public string FtpUserName { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string FtpRemotePath { get; set; } = "/";

    public DateTime? LastExportUtc { get; set; }
    public bool IsEnabled { get; set; }
    public bool HasCompletedSetup { get; set; }
    public bool RunAtStartup { get; set; } = true;

    public string BuildConnectionString() =>
        $"Dsn={OdbcDsn};Uid={OdbcUserId};Pwd={OdbcPassword}";
}
