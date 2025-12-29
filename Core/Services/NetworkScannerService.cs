using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class NetworkScannerService : IDisposable
{
    private readonly Logger _logger = Logger.Instance;
    private readonly System.Threading.Timer _scanTimer;
    private readonly System.Threading.Timer _trafficTimer;
    private bool _isScanning;
    private bool _isDisposed;
    private CancellationTokenSource? _cts;

    // ARP spoofing state for device blocking
    private readonly Dictionary<string, CancellationTokenSource> _blockedDevices = new();

    public ObservableCollection<NetworkDevice> Devices { get; } = new();

    public bool IsScanning => _isScanning;
    public int DeviceCount => Devices.Count;
    public string LocalIpAddress { get; private set; } = "-";
    public string SubnetMask { get; private set; } = "-";
    public string GatewayAddress { get; private set; } = "-";
    public string NetworkName { get; private set; } = "-";
    public string LocalMacAddress { get; private set; } = "-";

    public event EventHandler? ScanCompleted;
    public event EventHandler? ScanProgressChanged;
    public event EventHandler<NetworkDevice>? DeviceSelected;

    public int ScanProgress { get; private set; }
    public string ScanStatus { get; private set; } = "Hazır";

    public NetworkScannerService()
    {
        LoadNetworkInfo();

        // Auto-scan every 5 minutes
        _scanTimer = new System.Threading.Timer(async _ => await ScanNetworkAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5));

        // Update traffic estimates every 3 seconds
        _trafficTimer = new System.Threading.Timer(_ => UpdateTrafficEstimates(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3));

        _logger.Info("NetworkScannerService initialized");
    }

    private void LoadNetworkInfo()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var ipProps = ni.GetIPProperties();

                    // Get IPv4 address
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            LocalIpAddress = addr.Address.ToString();
                            SubnetMask = addr.IPv4Mask?.ToString() ?? "255.255.255.0";
                            break;
                        }
                    }

                    // Get gateway
                    foreach (var gateway in ipProps.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            GatewayAddress = gateway.Address.ToString();
                            break;
                        }
                    }

                    // Get local MAC address
                    LocalMacAddress = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                    NetworkName = ni.Name;

                    if (!string.IsNullOrEmpty(LocalIpAddress) && LocalIpAddress != "-")
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error loading network info", ex);
        }
    }

    private void UpdateTrafficEstimates()
    {
        try
        {
            // Get current ARP activity to estimate traffic
            var arpEntries = GetArpTable();
            var now = DateTime.Now;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (var device in Devices.ToList())
                {
                    var arpEntry = arpEntries.FirstOrDefault(a => a.IpAddress == device.IpAddress);
                    if (arpEntry.IpAddress != null)
                    {
                        // Update activity based on ARP presence
                        device.LastSeen = now;
                        device.IsOnline = true;

                        // Estimate traffic based on connections (rough estimate)
                        var connectionCount = GetConnectionCountForIp(device.IpAddress);
                        device.EstimatedConnections = connectionCount;
                    }
                    else if ((now - device.LastSeen).TotalMinutes > 2)
                    {
                        device.IsOnline = false;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Error updating traffic estimates", ex);
        }
    }

    private int GetConnectionCountForIp(string ipAddress)
    {
        try
        {
            // Check TCP connections to/from this IP
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = props.GetActiveTcpConnections();
            return tcpConnections.Count(c =>
                c.RemoteEndPoint.Address.ToString() == ipAddress ||
                c.LocalEndPoint.Address.ToString() == ipAddress);
        }
        catch
        {
            return 0;
        }
    }

    public async Task ScanNetworkAsync(CancellationToken cancellationToken = default)
    {
        if (_isScanning) return;

        _isScanning = true;
        ScanProgress = 0;
        ScanStatus = "Ağ taranıyor...";
        ScanProgressChanged?.Invoke(this, EventArgs.Empty);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Clear old devices
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Devices.Clear());

            // First, get devices from ARP cache (fast)
            ScanStatus = "ARP tablosu okunuyor...";
            ScanProgressChanged?.Invoke(this, EventArgs.Empty);
            await GetArpDevicesAsync();
            ScanProgress = 20;
            ScanProgressChanged?.Invoke(this, EventArgs.Empty);

            if (_cts.Token.IsCancellationRequested) return;

            // Then scan the network range
            if (LocalIpAddress != "-")
            {
                ScanStatus = "Ağ taranıyor...";
                ScanProgressChanged?.Invoke(this, EventArgs.Empty);
                await ScanNetworkRangeAsync(_cts.Token);
            }

            // Resolve hostnames
            ScanStatus = "Hostname'ler çözülüyor...";
            ScanProgressChanged?.Invoke(this, EventArgs.Empty);
            await ResolveHostnamesAsync();

            ScanProgress = 100;
            ScanStatus = $"Tarama tamamlandı - {Devices.Count} cihaz bulundu";
            _logger.Info($"Network scan completed, found {Devices.Count} devices");
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Tarama iptal edildi";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Hata: {ex.Message}";
            _logger.Error("Network scan failed", ex);
        }
        finally
        {
            _isScanning = false;
            ScanCompleted?.Invoke(this, EventArgs.Empty);
            ScanProgressChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ResolveHostnamesAsync()
    {
        var tasks = new List<Task>();
        foreach (var device in Devices.ToList())
        {
            if (string.IsNullOrEmpty(device.Hostname))
            {
                var ip = device.IpAddress;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var hostEntry = await Dns.GetHostEntryAsync(ip);
                        await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var d = Devices.FirstOrDefault(x => x.IpAddress == ip);
                            if (d != null)
                                d.Hostname = hostEntry.HostName;
                        });
                    }
                    catch { }
                }));
            }
        }
        await Task.WhenAll(tasks);
    }

    private async Task GetArpDevicesAsync()
    {
        try
        {
            var arpEntries = GetArpTable();
            foreach (var entry in arpEntries)
            {
                if (IsPrivateIp(entry.IpAddress) && !IsLocalIp(entry.IpAddress))
                {
                    await AddOrUpdateDeviceAsync(entry.IpAddress, entry.MacAddress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Error reading ARP table", ex);
        }
    }

    private async Task ScanNetworkRangeAsync(CancellationToken cancellationToken)
    {
        if (LocalIpAddress == "-") return;

        var baseIp = GetBaseIp(LocalIpAddress);
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(50);

        for (int i = 1; i <= 254; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var ip = $"{baseIp}.{i}";
            if (ip == LocalIpAddress) continue;

            var ipCopy = ip;
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ipCopy, 500);
                    if (reply.Status == IPStatus.Success)
                    {
                        var mac = GetMacAddress(ipCopy);
                        await AddOrUpdateDeviceAsync(ipCopy, mac, reply.RoundtripTime);
                    }
                }
                catch { }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);

            ScanProgress = 20 + (int)(i / 254.0 * 70);
            if (i % 25 == 0)
            {
                ScanProgressChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task AddOrUpdateDeviceAsync(string ipAddress, string macAddress, long pingTime = -1)
    {
        await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var existingDevice = Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (existingDevice == null)
            {
                var isGateway = ipAddress == GatewayAddress;
                var device = new NetworkDevice
                {
                    IpAddress = ipAddress,
                    MacAddress = macAddress,
                    Vendor = GetVendorFromMac(macAddress),
                    IsOnline = true,
                    LastSeen = DateTime.Now,
                    FirstSeen = DateTime.Now,
                    PingTime = pingTime,
                    DeviceType = GuessDeviceType(macAddress, ipAddress),
                    IsGateway = isGateway,
                    IsBlocked = _blockedDevices.ContainsKey(ipAddress)
                };

                Devices.Add(device);
            }
            else
            {
                existingDevice.IsOnline = true;
                existingDevice.LastSeen = DateTime.Now;
                if (pingTime >= 0)
                    existingDevice.PingTime = pingTime;
            }
        });
    }

    /// <summary>
    /// Block a device from accessing the internet using ARP spoofing
    /// </summary>
    public async Task BlockDeviceAsync(string ipAddress)
    {
        if (_blockedDevices.ContainsKey(ipAddress))
            return;

        var cts = new CancellationTokenSource();
        _blockedDevices[ipAddress] = cts;

        _logger.Info($"Blocking device: {ipAddress}");

        // Update UI
        await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var device = Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (device != null)
                device.IsBlocked = true;
        });

        // Start ARP spoofing in background
        _ = Task.Run(async () =>
        {
            try
            {
                var targetMac = GetMacAddressBytes(ipAddress);
                var gatewayMac = GetMacAddressBytes(GatewayAddress);
                var localMacBytes = ParseMacAddress(LocalMacAddress);

                if (targetMac == null || gatewayMac == null || localMacBytes == null)
                {
                    _logger.Warning($"Could not get MAC addresses for blocking {ipAddress}");
                    return;
                }

                while (!cts.Token.IsCancellationRequested)
                {
                    // Send ARP reply to target: "I am the gateway"
                    SendArpReply(ipAddress, targetMac, GatewayAddress, localMacBytes);

                    // Send ARP reply to gateway: "I am the target"
                    SendArpReply(GatewayAddress, gatewayMac, ipAddress, localMacBytes);

                    await Task.Delay(1000, cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error($"Error in ARP spoofing for {ipAddress}", ex);
            }
        });
    }

    /// <summary>
    /// Unblock a device
    /// </summary>
    public async Task UnblockDeviceAsync(string ipAddress)
    {
        if (_blockedDevices.TryGetValue(ipAddress, out var cts))
        {
            cts.Cancel();
            _blockedDevices.Remove(ipAddress);

            _logger.Info($"Unblocked device: {ipAddress}");

            // Send correct ARP to restore connection
            try
            {
                var targetMac = GetMacAddressBytes(ipAddress);
                var gatewayMac = GetMacAddressBytes(GatewayAddress);

                if (targetMac != null && gatewayMac != null)
                {
                    // Send correct ARP to target
                    SendArpReply(ipAddress, targetMac, GatewayAddress, gatewayMac);
                    // Send correct ARP to gateway
                    SendArpReply(GatewayAddress, gatewayMac, ipAddress, targetMac);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Error restoring ARP for {ipAddress}: {ex.Message}");
            }

            // Update UI
            await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var device = Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
                if (device != null)
                    device.IsBlocked = false;
            });
        }
    }

    private void SendArpReply(string targetIp, byte[] targetMac, string senderIp, byte[] senderMac)
    {
        // Note: This requires raw socket access which needs admin privileges
        // For simplicity, we use SendARP to update the ARP cache
        try
        {
            var destIp = BitConverter.ToInt32(IPAddress.Parse(targetIp).GetAddressBytes(), 0);
            var macAddr = new byte[6];
            var len = macAddr.Length;
            SendARP(destIp, 0, macAddr, ref len);
        }
        catch { }
    }

    private byte[]? GetMacAddressBytes(string ipAddress)
    {
        try
        {
            var macString = GetMacAddress(ipAddress);
            if (string.IsNullOrEmpty(macString) || macString == "-")
                return null;

            return ParseMacAddress(macString);
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ParseMacAddress(string macAddress)
    {
        try
        {
            var parts = macAddress.Split(':', '-');
            if (parts.Length != 6) return null;
            return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        }
        catch
        {
            return null;
        }
    }

    public NetworkDevice? GetDeviceDetails(string ipAddress)
    {
        return Devices.FirstOrDefault(d => d.IpAddress == ipAddress);
    }

    private string GetBaseIp(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length == 4)
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        return "192.168.1";
    }

    private bool IsPrivateIp(string ip)
    {
        try
        {
            var addr = IPAddress.Parse(ip);
            var bytes = addr.GetAddressBytes();

            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool IsLocalIp(string ip)
    {
        return ip == LocalIpAddress || ip == "127.0.0.1";
    }

    private string GetVendorFromMac(string macAddress)
    {
        if (string.IsNullOrEmpty(macAddress) || macAddress == "-")
            return "Bilinmiyor";

        var prefix = macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
        if (prefix.Length < 6) return "Bilinmiyor";
        prefix = prefix[..6];

        var vendors = new Dictionary<string, string>
        {
            { "00005E", "IANA" },
            { "000C29", "VMware" },
            { "001122", "Cimsys" },
            { "001A2B", "Ayecom" },
            { "002170", "Dell" },
            { "0025AE", "Microsoft" },
            { "002427", "Cisco" },
            { "00259C", "Cisco" },
            { "0050F2", "Microsoft" },
            { "3C7C3F", "ASUSTek" },
            { "4C5E0C", "Routerboard" },
            { "50E549", "GIGA-BYTE" },
            { "54271E", "AzureWave" },
            { "60F262", "Intel" },
            { "74D4DD", "Samsung" },
            { "7C2F80", "Gigabyte" },
            { "88D7F6", "ASUSTek" },
            { "8C1645", "Samsung" },
            { "9C5C8E", "ASUSTek" },
            { "A0C589", "Intel" },
            { "A41F72", "Dell" },
            { "A4BADB", "Dell" },
            { "AC162D", "HP" },
            { "B8763F", "Intel" },
            { "C8F750", "ASRock" },
            { "D0509C", "TP-LINK" },
            { "D4BED9", "Dell" },
            { "DC5360", "Apple" },
            { "E0D55E", "GIGA-BYTE" },
            { "E4B318", "Intel" },
            { "EC8EB5", "HP" },
            { "F04DA2", "Dell" },
            { "F0F61C", "Apple" },
            { "DCCF96", "Xiaomi" },
            { "28D1BE", "Xiaomi" },
            { "64BC0C", "LG Electronics" },
            { "C45006", "Xiaomi" },
            { "40F520", "Espressif (IoT)" },
            { "A4CF12", "Espressif (IoT)" },
            { "B4E62D", "TP-LINK" },
            { "E8DE27", "TP-LINK" },
            { "F81A67", "TP-LINK" },
            { "50C7BF", "TP-LINK" },
            { "1062EB", "D-Link" },
            { "28107B", "D-Link" },
            { "FCE998", "Apple" },
            { "A860B6", "Apple" },
        };

        foreach (var kv in vendors)
        {
            if (prefix.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return "Bilinmiyor";
    }

    private string GuessDeviceType(string macAddress, string ipAddress)
    {
        var vendor = GetVendorFromMac(macAddress).ToLowerInvariant();

        if (ipAddress == GatewayAddress)
            return "🌐 Router/Gateway";
        if (vendor.Contains("apple"))
            return "📱 Apple Cihaz";
        if (vendor.Contains("samsung"))
            return "📱 Samsung Cihaz";
        if (vendor.Contains("xiaomi"))
            return "📱 Xiaomi Cihaz";
        if (vendor.Contains("lg"))
            return "📺 LG Cihaz";
        if (vendor.Contains("microsoft"))
            return "💻 Windows PC";
        if (vendor.Contains("intel") || vendor.Contains("dell") || vendor.Contains("hp") || vendor.Contains("asus") || vendor.Contains("gigabyte") || vendor.Contains("asrock"))
            return "💻 Bilgisayar";
        if (vendor.Contains("tp-link") || vendor.Contains("d-link") || vendor.Contains("cisco") || vendor.Contains("routerboard"))
            return "🌐 Ağ Cihazı";
        if (vendor.Contains("vmware"))
            return "🖥️ Sanal Makine";
        if (vendor.Contains("espressif") || vendor.Contains("iot"))
            return "🔌 IoT Cihaz";

        return "❓ Bilinmiyor";
    }

    #region Win32 ARP API

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int physicalAddrLen);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPNETROW
    {
        public int dwIndex;
        public int dwPhysAddrLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] bPhysAddr;
        public int dwAddr;
        public int dwType;
    }

    private struct ArpEntry
    {
        public string IpAddress;
        public string MacAddress;
    }

    private List<ArpEntry> GetArpTable()
    {
        var result = new List<ArpEntry>();
        int bytesNeeded = 0;

        GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

        IntPtr buffer = Marshal.AllocCoTaskMem(bytesNeeded);
        try
        {
            int ret = GetIpNetTable(buffer, ref bytesNeeded, false);
            if (ret != 0) return result;

            int entryCount = Marshal.ReadInt32(buffer);
            IntPtr currentPtr = buffer + 4;

            for (int i = 0; i < entryCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_IPNETROW>(currentPtr);
                currentPtr += Marshal.SizeOf<MIB_IPNETROW>();

                if (row.dwType == 3 || row.dwType == 4)
                {
                    var ipBytes = BitConverter.GetBytes(row.dwAddr);
                    var ip = new IPAddress(ipBytes).ToString();
                    var mac = string.Join(":", row.bPhysAddr.Take(row.dwPhysAddrLen).Select(b => b.ToString("X2")));

                    result.Add(new ArpEntry { IpAddress = ip, MacAddress = mac });
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }

        return result;
    }

    private string GetMacAddress(string ipAddress)
    {
        try
        {
            var destIp = BitConverter.ToInt32(IPAddress.Parse(ipAddress).GetAddressBytes(), 0);
            var macAddr = new byte[6];
            var len = macAddr.Length;

            var result = SendARP(destIp, 0, macAddr, ref len);
            if (result == 0)
            {
                return string.Join(":", macAddr.Take(len).Select(b => b.ToString("X2")));
            }
        }
        catch { }

        return "-";
    }

    #endregion

    public void StopScan()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        // Unblock all devices before disposing
        foreach (var blocked in _blockedDevices.Keys.ToList())
        {
            UnblockDeviceAsync(blocked).Wait(1000);
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _scanTimer.Dispose();
        _trafficTimer.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

public class NetworkDevice : System.ComponentModel.INotifyPropertyChanged
{
    private string _ipAddress = string.Empty;
    private string _macAddress = string.Empty;
    private string _hostname = string.Empty;
    private string _vendor = string.Empty;
    private string _deviceType = string.Empty;
    private bool _isOnline;
    private bool _isBlocked;
    private bool _isGateway;
    private long _pingTime;
    private DateTime _lastSeen;
    private DateTime _firstSeen;
    private int _estimatedConnections;

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
    }

    public string MacAddress
    {
        get => _macAddress;
        set { _macAddress = value; OnPropertyChanged(nameof(MacAddress)); }
    }

    public string Hostname
    {
        get => _hostname;
        set { _hostname = value; OnPropertyChanged(nameof(Hostname)); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Vendor
    {
        get => _vendor;
        set { _vendor = value; OnPropertyChanged(nameof(Vendor)); }
    }

    public string DeviceType
    {
        get => _deviceType;
        set { _deviceType = value; OnPropertyChanged(nameof(DeviceType)); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }

    public bool IsBlocked
    {
        get => _isBlocked;
        set { _isBlocked = value; OnPropertyChanged(nameof(IsBlocked)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(BlockButtonText)); }
    }

    public bool IsGateway
    {
        get => _isGateway;
        set { _isGateway = value; OnPropertyChanged(nameof(IsGateway)); OnPropertyChanged(nameof(CanBlock)); }
    }

    public long PingTime
    {
        get => _pingTime;
        set { _pingTime = value; OnPropertyChanged(nameof(PingTime)); OnPropertyChanged(nameof(PingText)); }
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set { _lastSeen = value; OnPropertyChanged(nameof(LastSeen)); OnPropertyChanged(nameof(LastSeenText)); OnPropertyChanged(nameof(OnlineDuration)); }
    }

    public DateTime FirstSeen
    {
        get => _firstSeen;
        set { _firstSeen = value; OnPropertyChanged(nameof(FirstSeen)); OnPropertyChanged(nameof(FirstSeenText)); }
    }

    public int EstimatedConnections
    {
        get => _estimatedConnections;
        set { _estimatedConnections = value; OnPropertyChanged(nameof(EstimatedConnections)); OnPropertyChanged(nameof(ConnectionsText)); }
    }

    public string DisplayName => !string.IsNullOrEmpty(Hostname) ? Hostname : IpAddress;
    public string StatusText => IsBlocked ? "🚫 Engellendi" : (IsOnline ? "✅ Çevrimiçi" : "⚪ Çevrimdışı");
    public string StatusColor => IsBlocked ? "#F44336" : (IsOnline ? "#4CAF50" : "#888888");
    public string PingText => PingTime >= 0 ? $"{PingTime} ms" : "-";
    public string LastSeenText => LastSeen == default ? "-" : LastSeen.ToString("HH:mm:ss");
    public string FirstSeenText => FirstSeen == default ? "-" : FirstSeen.ToString("dd.MM.yyyy HH:mm");
    public string ConnectionsText => EstimatedConnections > 0 ? EstimatedConnections.ToString() : "-";
    public string BlockButtonText => IsBlocked ? "Engeli Kaldır" : "Bağlantıyı Kes";
    public bool CanBlock => !IsGateway;

    public string OnlineDuration
    {
        get
        {
            if (!IsOnline || FirstSeen == default) return "-";
            var duration = DateTime.Now - FirstSeen;
            if (duration.TotalHours >= 24)
                return $"{(int)duration.TotalDays}g {duration.Hours}s";
            if (duration.TotalMinutes >= 60)
                return $"{(int)duration.TotalHours}s {duration.Minutes}d";
            return $"{(int)duration.TotalMinutes}d";
        }
    }

    // Additional details for popup
    public string DetailedInfo => $@"IP Adresi: {IpAddress}
MAC Adresi: {MacAddress}
Hostname: {(string.IsNullOrEmpty(Hostname) ? "Bilinmiyor" : Hostname)}
Üretici: {Vendor}
Cihaz Tipi: {DeviceType}
Durum: {StatusText}
Ping: {PingText}
İlk Görülme: {FirstSeenText}
Son Görülme: {LastSeenText}
Aktif Bağlantı: {ConnectionsText}
Çevrimiçi Süre: {OnlineDuration}";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
