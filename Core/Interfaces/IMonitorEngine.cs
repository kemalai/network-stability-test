using InternetMonitor.Core.Models;

namespace InternetMonitor.Core.Interfaces;

public interface IMonitorEngine
{
    bool IsRunning { get; }
    NetworkMetrics CurrentMetrics { get; }

    event EventHandler<PingResult>? PingCompleted;
    event EventHandler<NetworkMetrics>? MetricsUpdated;
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    Task StartAsync();
    Task StopAsync();
    void Pause();
    void Resume();
}
