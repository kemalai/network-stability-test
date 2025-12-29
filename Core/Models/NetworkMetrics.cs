namespace InternetMonitor.Core.Models;

public class NetworkMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Ping metrikleri
    public long CurrentPing { get; set; }
    public long AveragePing { get; set; }
    public long MinPing { get; set; }
    public long MaxPing { get; set; }

    // Jitter (ping dalgalanması)
    public double Jitter { get; set; }

    // Packet loss
    public double PacketLossPercent { get; set; }
    public int TotalPacketsSent { get; set; }
    public int TotalPacketsLost { get; set; }

    // Bağlantı durumu
    public bool IsConnected { get; set; }
    public ConnectionState State { get; set; }

    // Uptime
    public TimeSpan CurrentSessionUptime { get; set; }
    public TimeSpan TotalUptime { get; set; }
    public TimeSpan TotalDowntime { get; set; }
    public double UptimePercent =>
        TotalUptime.TotalSeconds + TotalDowntime.TotalSeconds > 0
            ? (TotalUptime.TotalSeconds / (TotalUptime.TotalSeconds + TotalDowntime.TotalSeconds)) * 100
            : 100;

    // Son kesinti bilgisi
    public DateTime? LastDisconnectTime { get; set; }
    public DateTime? LastReconnectTime { get; set; }
    public TimeSpan? LastDowntimeDuration { get; set; }
}

public enum ConnectionState
{
    Unknown,
    Connected,
    Disconnected,
    Unstable,
    Reconnecting
}
