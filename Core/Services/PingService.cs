using System.Net.NetworkInformation;
using InternetMonitor.Core.Interfaces;
using InternetMonitor.Core.Models;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class PingService : IPingService
{
    private readonly Logger _logger = Logger.Instance;

    public async Task<PingResult> PingAsync(string target, int timeoutMs = 3000)
    {
        return await PingAsync(target, timeoutMs, 32);
    }

    public async Task<PingResult> PingAsync(string target, int timeoutMs, int bufferSize)
    {
        try
        {
            using var ping = new Ping();
            var buffer = new byte[bufferSize];
            var options = new PingOptions(128, true);

            var reply = await ping.SendPingAsync(target, timeoutMs, buffer, options);
            var result = PingResult.FromPingReply(reply, target);

            if (!result.Success)
            {
                _logger.Debug($"Ping failed to {target}: {result.Status}");
            }

            return result;
        }
        catch (PingException ex)
        {
            _logger.Error($"PingException for {target}", ex);
            return PingResult.Failed(target, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error pinging {target}", ex);
            return PingResult.Failed(target, ex.Message);
        }
    }

    public async Task<List<PingResult>> PingMultipleAsync(string[] targets, int timeoutMs = 3000)
    {
        var tasks = targets.Select(t => PingAsync(t, timeoutMs));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public double CalculateJitter(IEnumerable<long> pingTimes)
    {
        var times = pingTimes.Where(t => t >= 0).ToList();
        if (times.Count < 2) return 0;

        double totalDiff = 0;
        for (int i = 1; i < times.Count; i++)
        {
            totalDiff += Math.Abs(times[i] - times[i - 1]);
        }

        return totalDiff / (times.Count - 1);
    }

    public double CalculatePacketLoss(int sent, int received)
    {
        if (sent == 0) return 0;
        return ((double)(sent - received) / sent) * 100;
    }
}
