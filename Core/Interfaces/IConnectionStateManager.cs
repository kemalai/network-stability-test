using InternetMonitor.Core.Models;

namespace InternetMonitor.Core.Interfaces;

public interface IConnectionStateManager
{
    ConnectionState CurrentState { get; }
    bool IsConnected { get; }
    DateTime? LastStateChange { get; }
    TimeSpan CurrentStateDuration { get; }
    TimeSpan TotalUptime { get; }
    TimeSpan TotalDowntime { get; }

    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    void UpdateState(bool isConnected, long pingMs, double packetLossPercent);
    void Reset();
    NetworkMetrics GetCurrentMetrics();
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState OldState { get; init; }
    public ConnectionState NewState { get; init; }
    public DateTime Timestamp { get; init; }
    public TimeSpan? PreviousStateDuration { get; init; }
}
