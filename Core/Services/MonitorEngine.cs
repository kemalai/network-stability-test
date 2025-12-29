using InternetMonitor.Core.Interfaces;
using InternetMonitor.Core.Models;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class MonitorEngine : IMonitorEngine, IDisposable
{
    private readonly IPingService _pingService;
    private readonly IConnectionStateManager _connectionStateManager;
    private readonly AppSettings _settings;
    private readonly Logger _logger;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private bool _isPaused;
    private bool _isDisposed;

    public bool IsRunning => _monitoringTask != null && !_monitoringTask.IsCompleted;
    public NetworkMetrics CurrentMetrics => _connectionStateManager.GetCurrentMetrics();

    public event EventHandler<PingResult>? PingCompleted;
    public event EventHandler<NetworkMetrics>? MetricsUpdated;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public MonitorEngine() : this(new PingService(), new ConnectionStateManager())
    {
    }

    public MonitorEngine(IPingService pingService, IConnectionStateManager connectionStateManager)
    {
        _pingService = pingService;
        _connectionStateManager = connectionStateManager;
        _settings = AppSettings.Instance;
        _logger = Logger.Instance;

        _connectionStateManager.StateChanged += OnConnectionStateChanged;
    }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            _logger.Warning("Monitor engine is already running");
            return;
        }

        _logger.Info("Starting monitor engine...");
        _cancellationTokenSource = new CancellationTokenSource();
        _isPaused = false;

        _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cancellationTokenSource.Token));

        await Task.CompletedTask;
        _logger.Info("Monitor engine started");
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            _logger.Warning("Monitor engine is not running");
            return;
        }

        _logger.Info("Stopping monitor engine...");
        _cancellationTokenSource?.Cancel();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _monitoringTask = null;

        _logger.Info("Monitor engine stopped");
    }

    public void Pause()
    {
        _isPaused = true;
        _logger.Info("Monitor engine paused");
    }

    public void Resume()
    {
        _isPaused = false;
        _logger.Info("Monitor engine resumed");
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_isPaused)
                {
                    await PerformPingAsync();
                }

                await Task.Delay(_settings.PingIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Error in monitoring loop", ex);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task PerformPingAsync()
    {
        _logger.Debug($"Performing ping to {_settings.PingTarget}...");

        var result = await _pingService.PingAsync(
            _settings.PingTarget,
            _settings.PingTimeoutMs,
            _settings.PingBufferSize
        );

        _logger.Debug($"Ping result: Success={result.Success}, RTT={result.RoundtripTime}ms");

        // İlk hedef başarısız olursa yedek hedefi dene
        if (!result.Success)
        {
            var backupResult = await _pingService.PingAsync(
                _settings.SecondaryPingTarget,
                _settings.PingTimeoutMs
            );

            // Yedek başarılıysa onu kullan
            if (backupResult.Success)
            {
                result = backupResult;
            }
        }

        // State manager'ı güncelle
        var metrics = _connectionStateManager.GetCurrentMetrics();
        _connectionStateManager.UpdateState(
            result.Success,
            result.RoundtripTime,
            metrics.PacketLossPercent
        );

        // Event'leri tetikle
        _logger.Debug($"Firing PingCompleted event, handlers: {(PingCompleted != null ? "yes" : "no")}");
        PingCompleted?.Invoke(this, result);

        var updatedMetrics = _connectionStateManager.GetCurrentMetrics();
        _logger.Debug($"Firing MetricsUpdated event, AvgPing={updatedMetrics.AveragePing}ms, handlers: {(MetricsUpdated != null ? "yes" : "no")}");
        MetricsUpdated?.Invoke(this, updatedMetrics);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        ConnectionStateChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _connectionStateManager.StateChanged -= OnConnectionStateChanged;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}
