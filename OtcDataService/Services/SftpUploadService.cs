using System.IO;
using OtcDataService.Models;
using Renci.SshNet;

namespace OtcDataService.Services;

public sealed class SftpUploadService
{
    public static bool ValidateSettings(AppConfiguration config, out string? errorMessage)
    {
        if (!config.FtpUploadEnabled)
        {
            errorMessage = null;
            return true;
        }

        if (string.IsNullOrWhiteSpace(config.FtpHost))
        {
            errorMessage = "Host is required when remote upload is enabled.";
            return false;
        }

        if (config.FtpPort is <= 0 or > 65535)
        {
            errorMessage = "Port must be between 1 and 65535.";
            return false;
        }

        if (config.UploadLogonType == UploadLogonType.Normal &&
            string.IsNullOrWhiteSpace(config.FtpUserName))
        {
            errorMessage = "Username is required for Normal logon.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public async Task<bool> TestConnectionAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        if (!ValidateSettings(config, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        if (!config.FtpUploadEnabled)
        {
            return true;
        }

        using var client = CreateClient(config);
        await ConnectAsync(client, cancellationToken);

        var remotePath = NormalizeRemoteDirectory(config.FtpRemotePath);
        if (!string.IsNullOrEmpty(remotePath))
        {
            EnsureRemoteDirectory(client, remotePath);
        }

        return true;
    }

    public async Task UploadFileAsync(
        AppConfiguration config,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!config.FtpUploadEnabled)
        {
            return;
        }

        if (!ValidateSettings(config, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        var fileName = Path.GetFileName(localFilePath);
        var remoteDirectory = NormalizeRemoteDirectory(config.FtpRemotePath);
        var remotePath = string.IsNullOrEmpty(remoteDirectory)
            ? $"/{fileName}"
            : $"/{remoteDirectory}/{fileName}";

        using var client = CreateClient(config);
        await ConnectAsync(client, cancellationToken);

        if (!string.IsNullOrEmpty(remoteDirectory))
        {
            EnsureRemoteDirectory(client, remoteDirectory);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var fileStream = File.OpenRead(localFilePath);
        client.UploadFile(fileStream, remotePath, true);
    }

    private static SftpClient CreateClient(AppConfiguration config)
    {
        var (userName, password) = ResolveCredentials(config);
        var connectionInfo = new ConnectionInfo(
            config.FtpHost.Trim(),
            config.FtpPort,
            userName,
            new PasswordAuthenticationMethod(userName, password));

        return new SftpClient(connectionInfo);
    }

    private static async Task ConnectAsync(SftpClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Run(() => client.Connect(), cancellationToken);
    }

    private static (string UserName, string Password) ResolveCredentials(AppConfiguration config)
    {
        if (config.UploadLogonType == UploadLogonType.Anonymous)
        {
            return ("anonymous", string.Empty);
        }

        return (config.FtpUserName.Trim(), config.FtpPassword);
    }

    private static void EnsureRemoteDirectory(SftpClient client, string remoteDirectory)
    {
        var segments = remoteDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var segment in segments)
        {
            current = string.IsNullOrEmpty(current) ? segment : $"{current}/{segment}";
            var remotePath = $"/{current}";
            if (!client.Exists(remotePath))
            {
                client.CreateDirectory(remotePath);
            }
        }
    }

    private static string NormalizeRemoteDirectory(string? remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return string.Empty;
        }

        return remotePath.Trim().Replace('\\', '/').Trim('/');
    }
}
