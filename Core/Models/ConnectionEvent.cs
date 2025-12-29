namespace InternetMonitor.Core.Models;

public class ConnectionEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ConnectionEventType EventType { get; set; }
    public string? Description { get; set; }
    public TimeSpan? Duration { get; set; }
    public long? LastPingBeforeEvent { get; set; }
    public double? PacketLossAtEvent { get; set; }
}

public enum ConnectionEventType
{
    Connected,
    Disconnected,
    HighLatency,
    PacketLoss,
    Recovered,
    Unstable
}
