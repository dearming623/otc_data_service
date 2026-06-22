using OtcDataService.Models;

namespace OtcDataService.Services;

public sealed class RemoteUploadService
{
    private readonly FtpUploadService _ftpUploadService;
    private readonly SftpUploadService _sftpUploadService;

    public RemoteUploadService(FtpUploadService ftpUploadService, SftpUploadService sftpUploadService)
    {
        _ftpUploadService = ftpUploadService;
        _sftpUploadService = sftpUploadService;
    }

    public static bool ValidateSettings(AppConfiguration config, out string? errorMessage) =>
        config.UploadProtocol == UploadProtocol.Sftp
            ? SftpUploadService.ValidateSettings(config, out errorMessage)
            : FtpUploadService.ValidateSettings(config, out errorMessage);

    public Task<bool> TestConnectionAsync(AppConfiguration config, CancellationToken cancellationToken = default) =>
        config.UploadProtocol == UploadProtocol.Sftp
            ? _sftpUploadService.TestConnectionAsync(config, cancellationToken)
            : _ftpUploadService.TestConnectionAsync(config, cancellationToken);

    public Task UploadFileAsync(
        AppConfiguration config,
        string localFilePath,
        CancellationToken cancellationToken = default) =>
        config.UploadProtocol == UploadProtocol.Sftp
            ? _sftpUploadService.UploadFileAsync(config, localFilePath, cancellationToken)
            : _ftpUploadService.UploadFileAsync(config, localFilePath, cancellationToken);

    public static string GetProtocolLabel(UploadProtocol protocol) =>
        protocol == UploadProtocol.Sftp ? "SFTP" : "FTP";
}
