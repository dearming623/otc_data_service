using OtcDataService.Repositories;

namespace OtcDataService.Services;

public static class AppServices
{
    public static ConfigurationService Configuration { get; } = new();
    public static LogService Log { get; } = new();
    public static OdbcConnectionService Odbc { get; } = new(Log);
    public static ICleanupTrnRepository CleanupTrn { get; } = new CleanupTrnRepository(Configuration, Odbc);
    public static IProdtableRepository Prodtable { get; } = new ProdtableRepository(Configuration, Odbc);
    public static IMktDepRepository MktDep { get; } = new MktDepRepository(Configuration, Odbc);
    public static IItemCategoryRepository ItemCategory { get; } = new ItemCategoryRepository(Configuration, Odbc);
    public static FtpUploadService FtpUploadBackend { get; } = new();
    public static SftpUploadService SftpUpload { get; } = new();
    public static RemoteUploadService RemoteUpload { get; } = new(FtpUploadBackend, SftpUpload);
    public static ExportDataService ExportData { get; } = new(
        Configuration,
        CleanupTrn,
        Prodtable,
        MktDep,
        ItemCategory,
        RemoteUpload,
        Log);
    public static OtcExportScheduler ExportScheduler { get; } = new(Configuration, Odbc, ExportData, Log);
    public static StartupService Startup { get; } = new();
}
