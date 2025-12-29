using InternetMonitor.Core.Interfaces;
using InternetMonitor.Core.Models;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class ConnectionStateManager : IConnectionStateManager
{
    private readonly Logger _logger = Logger.Instance;
    private readonly AppSettings _settings = AppSettings.Instance;
    private readonly object _lock = new();

    private ConnectionState _currentState = ConnectionState.Unknown;
    private DateTime? _lastStateChange;
    private DateTime _monitoringStartTime;
    private TimeSpan _totalUptime = TimeSpan.Zero;
    private TimeSpan _totalDowntime = TimeSpan.Zero;
    private DateTime? _lastDisconnectTime;
    private DateTime? _lastReconnectTime;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;

    private readonly Queue<long> _recentPings = new();
    private readonly Queue<bool> _recentResults = new();
    private const int MaxSamples = 100;

    public ConnectionState CurrentState
    {
        get { lock (_lock) return _currentState; }
    }

    public bool IsConnected
    {
        get { lock (_lock) return _currentState == ConnectionState.Connected; }
    }

    public DateTime? LastStateChange
    {
        get { lock (_lock) return _lastStateChange; }
    }

    public TimeSpan CurrentStateDuration
    {
        get
        {
            lock (_lock)
            {
                if (_lastStateChange == null) return TimeSpan.Zero;
                return DateTime.Now - _lastStateChange.Value;
            }
        }
    }

    public TimeSpan TotalUptime
    {
        get
        {
            lock (_lock)
            {
                var uptime = _totalUptime;
                if (_currentState == ConnectionState.Connected && _lastStateChange.HasValue)
                {
                    uptime += DateTime.Now - _lastStateChange.Value;
                }
                return uptime;
            }
        }
    }

    public TimeSpan TotalDowntime
    {
        get
        {
            lock (_lock)
            {
                var downtime = _totalDowntime;
                if (_currentState == ConnectionState.Disconnected && _lastStateChange.HasValue)
                {
                    downtime += DateTime.Now - _lastStateChange.Value;
                }
                return downtime;
            }
        }
    }

    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public ConnectionStateManager()
    {
        _monitoringStartTime = DateTime.Now;
    }

    public void UpdateState(bool isConnected, long pingMs, double packetLossPercent)
    {
        lock (_lock)
        {
            // Örnekleri sakla
            if (pingMs >= 0)
            {
                _recentPings.Enqueue(pingMs);
                if (_recentPings.Count > MaxSamples) _recentPings.Dequeue();
            }

            _recentResults.Enqueue(isConnected);
            if (_recentResults.Count > MaxSamples) _recentResults.Dequeue();

            // Ardışık başarı/başarısızlık sayıları
            if (isConnected)
            {
                _consecutiveSuccesses++;
                _consecutiveFailures = 0;
            }
            else
            {
                _consecutiveFailures++;
                _consecutiveSuccesses = 0;
            }

            // Yeni durumu belirle
            var newState = DetermineState(isConnected, pingMs, packetLossPercent);

            if (newState != _currentState)
            {
                var oldState = _currentState;
                var previousDuration = CurrentStateDuration;

                // Uptime/downtime hesapla
                if (_lastStateChange.HasValue)
                {
                    if (oldState == ConnectionState.Connected)
                    {
                        _totalUptime += previousDuration;
                    }
                    else if (oldState == ConnectionState.Disconnected)
                    {
                        _totalDowntime += previousDuration;
                    }
                }

                // Bağlantı durumu değişikliklerini kaydet
                if (newState == ConnectionState.Disconnected)
                {
                    _lastDisconnectTime = DateTime.Now;
                }
                else if (oldState == ConnectionState.Disconnected && newState == ConnectionState.Connected)
                {
                    _lastReconnectTime = DateTime.Now;
                }

                _currentState = newState;
                _lastStateChange = DateTime.Now;

                _logger.Info($"Connection state changed: {oldState} -> {newState}");

                StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    OldState = oldState,
                    NewState = newState,
                    Timestamp = DateTime.Now,
                    PreviousStateDuration = previousDuration
                });
            }
        }
    }

    private ConnectionState DetermineState(bool isConnected, long pingMs, double packetLossPercent)
    {
        // 3 ardışık başarısızlık = Disconnected
        if (_consecutiveFailures >= 3)
        {
            return ConnectionState.Disconnected;
        }

        // Bağlantı kuruldu
        if (!isConnected)
        {
            if (_currentState == ConnectionState.Connected || _currentState == ConnectionState.Unstable)
            {
                return ConnectionState.Unstable;
            }
            return ConnectionState.Disconnected;
        }

        // Yüksek latency veya packet loss = Unstable
        if (pingMs > _settings.HighLatencyThresholdMs || packetLossPercent > _settings.PacketLossThresholdPercent)
        {
            return ConnectionState.Unstable;
        }

        // 3 ardışık başarı = Connected
        if (_consecutiveSuccesses >= 3 || _currentState == ConnectionState.Unknown)
        {
            return ConnectionState.Connected;
        }

        return _currentState == ConnectionState.Unknown ? ConnectionState.Connected : _currentState;
    }

    public NetworkMetrics GetCurrentMetrics()
    {
        lock (_lock)
        {
            var pingList = _recentPings.ToList();
            var successCount = _recentResults.Count(r => r);
            var totalCount = _recentResults.Count;

            return new NetworkMetrics
            {
                Timestamp = DateTime.Now,
                CurrentPing = pingList.LastOrDefault(),
                AveragePing = pingList.Count > 0 ? (long)pingList.Average() : 0,
                MinPing = pingList.Count > 0 ? pingList.Min() : 0,
                MaxPing = pingList.Count > 0 ? pingList.Max() : 0,
                Jitter = CalculateJitter(pingList),
                PacketLossPercent = totalCount > 0 ? ((double)(totalCount - successCount) / totalCount) * 100 : 0,
                TotalPacketsSent = totalCount,
                TotalPacketsLost = totalCount - successCount,
                IsConnected = _currentState == ConnectionState.Connected,
                State = _currentState,
                CurrentSessionUptime = CurrentStateDuration,
                TotalUptime = TotalUptime,
                TotalDowntime = TotalDowntime,
                LastDisconnectTime = _lastDisconnectTime,
                LastReconnectTime = _lastReconnectTime
            };
        }
    }

    private double CalculateJitter(List<long> pings)
    {
        if (pings.Count < 2) return 0;

        double totalDiff = 0;
        for (int i = 1; i < pings.Count; i++)
        {
            totalDiff += Math.Abs(pings[i] - pings[i - 1]);
        }
        return totalDiff / (pings.Count - 1);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentState = ConnectionState.Unknown;
            _lastStateChange = null;
            _monitoringStartTime = DateTime.Now;
            _totalUptime = TimeSpan.Zero;
            _totalDowntime = TimeSpan.Zero;
            _lastDisconnectTime = null;
            _lastReconnectTime = null;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
            _recentPings.Clear();
            _recentResults.Clear();

            _logger.Info("Connection state manager reset");
        }
    }
}
