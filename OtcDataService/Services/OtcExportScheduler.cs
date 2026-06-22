namespace OtcDataService.Services;

public sealed class OtcExportScheduler : IDisposable
{
    private readonly ConfigurationService _configurationService;
    private readonly OdbcConnectionService _odbcConnectionService;
    private readonly ExportDataService _exportDataService;
    private readonly LogService _logService;

    private readonly object _sync = new();
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _isRunning;

    public event EventHandler<bool>? RunningStateChanged;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public OtcExportScheduler(
        ConfigurationService configurationService,
        OdbcConnectionService odbcConnectionService,
        ExportDataService exportDataService,
        LogService logService)
    {
        _configurationService = configurationService;
        _odbcConnectionService = odbcConnectionService;
        _exportDataService = exportDataService;
        _logService = logService;
    }

    public bool TryStart(out string? errorMessage)
    {
        lock (_sync)
        {
            if (_isRunning)
            {
                errorMessage = null;
                return true;
            }

            var config = _configurationService.Current;
            if (string.IsNullOrWhiteSpace(config.OdbcDsn))
            {
                errorMessage = "ODBC DSN is required.";
                return false;
            }

            if (config.SalesLookbackDays <= 0)
            {
                errorMessage = "Sales lookback days must be greater than zero.";
                return false;
            }

            if (config.DocumentIntervalDays <= 0)
            {
                errorMessage = "Document interval days must be greater than zero.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.OutputFolder))
            {
                errorMessage = "Output folder is required.";
                return false;
            }

            if (!RemoteUploadService.ValidateSettings(config, out errorMessage))
            {
                return false;
            }

            if (!_odbcConnectionService.TestConnection(config, out errorMessage))
            {
                return false;
            }

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(TimeSpan.FromHours(1));
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
            _isRunning = true;

            _configurationService.Update(c => c.IsEnabled = true);
            _logService.Info("Export scheduler enabled.");
            RunningStateChanged?.Invoke(this, true);

            _ = Task.Run(() => TryRunExportIfDueAsync(_cts.Token));
            errorMessage = null;
            return true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            _cts?.Cancel();
            _timer?.Dispose();
            _timer = null;
            _isRunning = false;

            _configurationService.Update(c => c.IsEnabled = false);
            _logService.Info("Export scheduler disabled.");
            RunningStateChanged?.Invoke(this, false);
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        if (_timer is null)
        {
            return;
        }

        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                await TryRunExportIfDueAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task TryRunExportIfDueAsync(CancellationToken cancellationToken)
    {
        if (!IsDueForExport())
        {
            return;
        }

        await _exportDataService.ExportAsync(cancellationToken);
    }

    private bool IsDueForExport()
    {
        var config = _configurationService.Current;
        if (config.LastExportUtc is null)
        {
            return true;
        }

        var elapsed = DateTime.UtcNow - config.LastExportUtc.Value;
        return elapsed.TotalDays >= config.DocumentIntervalDays;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
