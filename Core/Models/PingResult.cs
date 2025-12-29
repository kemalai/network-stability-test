using System.Net.NetworkInformation;

namespace InternetMonitor.Core.Models;

public class PingResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Target { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long RoundtripTime { get; set; }
    public IPStatus Status { get; set; }
    public int Ttl { get; set; }
    public string? ErrorMessage { get; set; }

    public static PingResult FromPingReply(PingReply reply, string target)
    {
        return new PingResult
        {
            Timestamp = DateTime.Now,
            Target = target,
            Success = reply.Status == IPStatus.Success,
            RoundtripTime = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1,
            Status = reply.Status,
            Ttl = reply.Status == IPStatus.Success ? reply.Options?.Ttl ?? 0 : 0
        };
    }

    public static PingResult Failed(string target, string errorMessage)
    {
        return new PingResult
        {
            Timestamp = DateTime.Now,
            Target = target,
            Success = false,
            RoundtripTime = -1,
            Status = IPStatus.Unknown,
            ErrorMessage = errorMessage
        };
    }
}
