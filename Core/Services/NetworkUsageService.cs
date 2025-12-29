using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class NetworkUsageService : IDisposable
{
    private readonly Logger _logger = Logger.Instance;
    private readonly System.Threading.Timer _updateTimer;
    private readonly Dictionary<int, ProcessNetworkStats> _processStats = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    // Performance counters for accurate measurement
    private PerformanceCounter? _bytesReceivedCounter;
    private PerformanceCounter? _bytesSentCounter;
    private string? _activeNetworkInterface;

    // Previous values for delta calculation
    private long _lastTotalBytesReceived;
    private long _lastTotalBytesSent;
    private DateTime _lastUpdate = DateTime.Now;

    // TCP connection tracking for bandwidth distribution
    private readonly Dictionary<int, ConnectionTracker> _connectionTrackers = new();

    public ObservableCollection<NetworkProcessInfo> NetworkProcesses { get; } = new();

    public string TotalDownloadSpeed { get; private set; } = "0 KB/s";
    public string TotalUploadSpeed { get; private set; } = "0 KB/s";
    public long TotalDownloadBytesPerSec { get; private set; }
    public long TotalUploadBytesPerSec { get; private set; }
    public int ActiveConnectionCount { get; private set; }
    public int ProcessCount { get; private set; }

    public event EventHandler? StatsUpdated;

    public NetworkUsageService()
    {
        InitializePerformanceCounters();

        // Initialize with current network stats
        var stats = GetNetworkInterfaceStats();
        _lastTotalBytesReceived = stats.bytesReceived;
        _lastTotalBytesSent = stats.bytesSent;

        // Update every 1 second for more accurate readings
        _updateTimer = new System.Threading.Timer(UpdateStats, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        _logger.Info("NetworkUsageService initialized with performance counters");
    }

    private void InitializePerformanceCounters()
    {
        try
        {
            // Find active network interface
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var stats = ni.GetIPv4Statistics();
                    if (stats.BytesReceived > 0 || stats.BytesSent > 0)
                    {
                        _activeNetworkInterface = ni.Description;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_activeNetworkInterface))
            {
                // Try to create performance counters
                try
                {
                    _bytesReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", _activeNetworkInterface);
                    _bytesSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", _activeNetworkInterface);

                    // Initial read to prime the counters
                    _bytesReceivedCounter.NextValue();
                    _bytesSentCounter.NextValue();

                    _logger.Info($"Performance counters initialized for: {_activeNetworkInterface}");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Could not create performance counters: {ex.Message}");
                    _bytesReceivedCounter = null;
                    _bytesSentCounter = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error initializing performance counters", ex);
        }
    }

    private void UpdateStats(object? state)
    {
        try
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var elapsed = (now - _lastUpdate).TotalSeconds;
                if (elapsed < 0.5) return;

                // Get current network stats using multiple methods for accuracy
                long downloadSpeed = 0;
                long uploadSpeed = 0;

                // Method 1: Performance counters (most accurate, matches Task Manager)
                if (_bytesReceivedCounter != null && _bytesSentCounter != null)
                {
                    try
                    {
                        downloadSpeed = (long)_bytesReceivedCounter.NextValue();
                        uploadSpeed = (long)_bytesSentCounter.NextValue();
                    }
                    catch
                    {
                        // Fallback to Method 2
                        var stats = GetNetworkInterfaceStats();
                        var bytesReceivedDiff = stats.bytesReceived - _lastTotalBytesReceived;
                        var bytesSentDiff = stats.bytesSent - _lastTotalBytesSent;
                        downloadSpeed = (long)(bytesReceivedDiff / elapsed);
                        uploadSpeed = (long)(bytesSentDiff / elapsed);
                        _lastTotalBytesReceived = stats.bytesReceived;
                        _lastTotalBytesSent = stats.bytesSent;
                    }
                }
                else
                {
                    // Method 2: NetworkInterface stats
                    var stats = GetNetworkInterfaceStats();
                    var bytesReceivedDiff = stats.bytesReceived - _lastTotalBytesReceived;
                    var bytesSentDiff = stats.bytesSent - _lastTotalBytesSent;
                    downloadSpeed = (long)(bytesReceivedDiff / elapsed);
                    uploadSpeed = (long)(bytesSentDiff / elapsed);
                    _lastTotalBytesReceived = stats.bytesReceived;
                    _lastTotalBytesSent = stats.bytesSent;
                }

                TotalDownloadBytesPerSec = Math.Max(0, downloadSpeed);
                TotalUploadBytesPerSec = Math.Max(0, uploadSpeed);
                TotalDownloadSpeed = FormatSpeed(TotalDownloadBytesPerSec);
                TotalUploadSpeed = FormatSpeed(TotalUploadBytesPerSec);

                _lastUpdate = now;

                // Update per-process network info with accurate distribution
                UpdateProcessNetworkInfo(TotalDownloadBytesPerSec, TotalUploadBytesPerSec, elapsed);

                // Get active connection count
                try
                {
                    var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                    ActiveConnectionCount = tcpConnections.Count(c => c.State == TcpState.Established);
                }
                catch
                {
                    ActiveConnectionCount = 0;
                }
            }

            StatsUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Error("Error updating network stats", ex);
        }
    }

    private void UpdateProcessNetworkInfo(long totalDownloadSpeed, long totalUploadSpeed, double elapsed)
    {
        try
        {
            // Get TCP connections with their states and data
            var tcpConnections = GetTcpConnectionsWithStats();
            var processConnectionData = new Dictionary<int, ProcessConnectionData>();

            // Aggregate connection data by process
            foreach (var conn in tcpConnections)
            {
                if (conn.ProcessId <= 0) continue;

                if (!processConnectionData.TryGetValue(conn.ProcessId, out var data))
                {
                    data = new ProcessConnectionData { ProcessId = conn.ProcessId };
                    processConnectionData[conn.ProcessId] = data;
                }

                data.ConnectionCount++;
                data.TotalDataSize += conn.DataSize;

                if (conn.State == TcpState.Established)
                    data.EstablishedCount++;
            }

            // Calculate total weight for distribution
            long totalWeight = processConnectionData.Values.Sum(p => p.TotalDataSize + (p.EstablishedCount * 1000));
            if (totalWeight == 0) totalWeight = 1;

            // Update process stats
            var currentPids = new HashSet<int>();

            foreach (var (pid, connData) in processConnectionData)
            {
                currentPids.Add(pid);

                if (!_processStats.TryGetValue(pid, out var stats))
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        stats = new ProcessNetworkStats
                        {
                            ProcessId = pid,
                            ProcessName = process.ProcessName
                        };
                        _processStats[pid] = stats;
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Distribute bandwidth proportionally
                long weight = connData.TotalDataSize + (connData.EstablishedCount * 1000);
                double ratio = (double)weight / totalWeight;

                stats.CurrentDownloadSpeed = (long)(totalDownloadSpeed * ratio);
                stats.CurrentUploadSpeed = (long)(totalUploadSpeed * ratio);
                stats.ConnectionCount = connData.ConnectionCount;
                stats.TotalBytesReceived += (long)(stats.CurrentDownloadSpeed * elapsed);
                stats.TotalBytesSent += (long)(stats.CurrentUploadSpeed * elapsed);
            }

            // Remove processes that are no longer active
            var pidsToRemove = _processStats.Keys.Where(p => !currentPids.Contains(p)).ToList();
            foreach (var pid in pidsToRemove)
            {
                _processStats.Remove(pid);
            }

            // Update observable collection on UI thread
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                NetworkProcesses.Clear();

                var sorted = _processStats.Values
                    .Where(p => p.CurrentDownloadSpeed > 0 || p.CurrentUploadSpeed > 0 || p.ConnectionCount > 0)
                    .OrderByDescending(p => p.CurrentDownloadSpeed + p.CurrentUploadSpeed)
                    .ThenByDescending(p => p.ConnectionCount)
                    .Take(50);

                foreach (var proc in sorted)
                {
                    NetworkProcesses.Add(new NetworkProcessInfo
                    {
                        ProcessId = proc.ProcessId,
                        ProcessName = proc.ProcessName,
                        DownloadSpeed = FormatSpeed(proc.CurrentDownloadSpeed),
                        UploadSpeed = FormatSpeed(proc.CurrentUploadSpeed),
                        DownloadBytesPerSec = proc.CurrentDownloadSpeed,
                        UploadBytesPerSec = proc.CurrentUploadSpeed,
                        TotalDownloaded = FormatBytes(proc.TotalBytesReceived),
                        TotalUploaded = FormatBytes(proc.TotalBytesSent),
                        ConnectionCount = proc.ConnectionCount
                    });
                }

                ProcessCount = NetworkProcesses.Count;
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Error updating process network info", ex);
        }
    }

    private List<TcpConnectionWithStats> GetTcpConnectionsWithStats()
    {
        var result = new List<TcpConnectionWithStats>();

        try
        {
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);

            IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint ret = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
                if (ret != 0) return result;

                int rowCount = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = tcpTablePtr + 4;

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);

                    // Calculate estimated data based on ports and state
                    long dataSize = EstimateConnectionDataSize(row);

                    result.Add(new TcpConnectionWithStats
                    {
                        ProcessId = row.owningPid,
                        State = (TcpState)row.state,
                        LocalPort = (ushort)((row.localPort >> 8) | (row.localPort << 8)),
                        RemotePort = (ushort)((row.remotePort >> 8) | (row.remotePort << 8)),
                        DataSize = dataSize
                    });

                    rowPtr += Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }

            // Also check UDP
            bufferSize = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);

            IntPtr udpTablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint ret = GetExtendedUdpTable(udpTablePtr, ref bufferSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
                if (ret != 0) return result;

                int rowCount = Marshal.ReadInt32(udpTablePtr);
                IntPtr rowPtr = udpTablePtr + 4;

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);

                    result.Add(new TcpConnectionWithStats
                    {
                        ProcessId = row.owningPid,
                        State = TcpState.Established, // UDP is connectionless
                        LocalPort = (ushort)((row.localPort >> 8) | (row.localPort << 8)),
                        RemotePort = 0,
                        DataSize = 500 // Base estimate for UDP
                    });

                    rowPtr += Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(udpTablePtr);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error getting TCP connections with stats", ex);
        }

        return result;
    }

    private long EstimateConnectionDataSize(MIB_TCPROW_OWNER_PID row)
    {
        // Estimate data size based on connection state and common ports
        var state = (TcpState)row.state;
        ushort remotePort = (ushort)((row.remotePort >> 8) | (row.remotePort << 8));

        long baseSize = state switch
        {
            TcpState.Established => 1000,
            TcpState.SynSent => 100,
            TcpState.SynReceived => 100,
            TcpState.FinWait1 => 500,
            TcpState.FinWait2 => 500,
            TcpState.TimeWait => 200,
            TcpState.CloseWait => 300,
            TcpState.LastAck => 200,
            _ => 100
        };

        // Adjust for common high-bandwidth ports
        baseSize *= remotePort switch
        {
            80 or 8080 => 3,      // HTTP
            443 => 5,             // HTTPS (usually more data)
            22 => 1,              // SSH
            21 or 20 => 2,        // FTP
            3389 => 4,            // RDP
            _ when remotePort >= 6881 && remotePort <= 6999 => 10, // BitTorrent
            _ when remotePort >= 27000 && remotePort <= 27050 => 5, // Steam
            _ => 1
        };

        return baseSize;
    }

    private (long bytesReceived, long bytesSent) GetNetworkInterfaceStats()
    {
        long totalReceived = 0;
        long totalSent = 0;

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var stats = ni.GetIPv4Statistics();
                    totalReceived += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error getting network interface stats", ex);
        }

        return (totalReceived, totalSent);
    }

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 0) bytesPerSecond = 0;

        if (bytesPerSecond >= 1024 * 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
        if (bytesPerSecond >= 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        if (bytesPerSecond >= 1024)
            return $"{bytesPerSecond / 1024:F1} KB/s";
        return $"{bytesPerSecond:F0} B/s";
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 0) bytes = 0;

        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    public void Refresh()
    {
        UpdateStats(null);
    }

    #region Win32 API

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved);

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public int owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint localAddr;
        public uint localPort;
        public int owningPid;
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;

        _updateTimer.Dispose();
        _bytesReceivedCounter?.Dispose();
        _bytesSentCounter?.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

internal class ProcessNetworkStats
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int ConnectionCount { get; set; }
    public long TotalBytesReceived { get; set; }
    public long TotalBytesSent { get; set; }
    public long CurrentDownloadSpeed { get; set; }
    public long CurrentUploadSpeed { get; set; }
}

internal class ProcessConnectionData
{
    public int ProcessId { get; set; }
    public int ConnectionCount { get; set; }
    public int EstablishedCount { get; set; }
    public long TotalDataSize { get; set; }
}

internal class ConnectionTracker
{
    public int ProcessId { get; set; }
    public long LastBytesIn { get; set; }
    public long LastBytesOut { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.Now;
}

internal class TcpConnectionWithStats
{
    public int ProcessId { get; set; }
    public TcpState State { get; set; }
    public ushort LocalPort { get; set; }
    public ushort RemotePort { get; set; }
    public long DataSize { get; set; }
}

public class NetworkProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DownloadSpeed { get; set; } = "0 B/s";
    public string UploadSpeed { get; set; } = "0 B/s";
    public long DownloadBytesPerSec { get; set; }
    public long UploadBytesPerSec { get; set; }
    public string TotalDownloaded { get; set; } = "0 B";
    public string TotalUploaded { get; set; } = "0 B";
    public int ConnectionCount { get; set; }
}
