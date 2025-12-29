using InternetMonitor.Core.Models;

namespace InternetMonitor.Core.Interfaces;

public interface IPingService
{
    Task<PingResult> PingAsync(string target, int timeoutMs = 3000);
    Task<PingResult> PingAsync(string target, int timeoutMs, int bufferSize);
    Task<List<PingResult>> PingMultipleAsync(string[] targets, int timeoutMs = 3000);
    double CalculateJitter(IEnumerable<long> pingTimes);
    double CalculatePacketLoss(int sent, int received);
}
