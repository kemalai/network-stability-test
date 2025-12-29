# Internet Stability Monitor

A comprehensive Windows desktop application for monitoring internet connection stability, network usage, and performing various network diagnostics.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

## Features

### Real-Time Monitoring
- **Ping Monitoring** - Continuous ping monitoring with configurable targets and intervals
- **Connection State Tracking** - Automatic detection of connection states (Connected, Disconnected, Unstable, Reconnecting)
- **Live Charts** - Real-time visualization of ping latency and packet loss history
- **Event History** - Detailed log of all connection state changes with timestamps

### Network Metrics
- **Current Ping** - Real-time round-trip time measurement
- **Average Ping** - Running average of all ping measurements
- **Jitter** - Connection stability indicator showing ping variance
- **Packet Loss** - Percentage of failed ping requests
- **Uptime** - Total connection uptime and uptime percentage

### Network Usage Monitor
- **Process-Based Tracking** - Monitor network usage per application/process
- **Download/Upload Speed** - Real-time bandwidth usage per process
- **Connection Count** - Active connections per application
- **Total Data Transferred** - Cumulative data sent/received per process

### Speed Test
- **Multi-Server Support** - Test with multiple servers worldwide
- **Download Speed** - Accurate download speed measurement
- **Upload Speed** - Accurate upload speed measurement
- **Real-Time Graphs** - Live speed visualization during tests
- **Server Selection** - Choose from various test servers (Cloudflare, Hetzner, OVH, etc.)

### Website Monitor
- **Availability Checking** - Monitor multiple websites for uptime
- **Response Time** - Track website response times
- **Status Tracking** - Visual indicators for online/offline status

### Network Device Scanner
- **ARP Scanning** - Discover devices on your local network
- **Device Information** - IP address, hostname, MAC address, manufacturer
- **Device Details** - View detailed information about each device
- **Ping Status** - Check if devices are online

### Additional Features
- **Cache Cleaner** - Clear DNS cache and temporary network files
- **Multi-Language Support** - Available in 7 languages:
  - English
  - Turkish (Turkce)
  - Spanish (Espanol)
  - French (Francais)
  - German (Deutsch)
  - Portuguese (Portugues)
  - Russian (Russkiy)
- **System Tray Integration** - Minimize to system tray
- **Export Functionality** - Export data to CSV or generate summary reports
- **Configurable Settings** - Customize ping targets, intervals, thresholds, and more
- **Desktop Notifications** - Get notified on connection issues

## Screenshots

*Coming soon*

## Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime (included in self-contained builds)

## Installation

### Option 1: Download Release
1. Go to [Releases](https://github.com/kemalai/network-stability-test/releases)
2. Download the latest `InternetMonitor-win-x64.zip`
3. Extract and run `InternetMonitor.exe`

### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/kemalai/network-stability-test.git
cd network-stability-test

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run
dotnet run -c Release

# Or publish self-contained
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

## Usage

1. **Launch the application** - The monitoring starts automatically
2. **Dashboard Tab** - View real-time ping metrics and charts
3. **Network Usage Tab** - Monitor per-process network usage
4. **Speed Test Tab** - Run internet speed tests
5. **Websites Tab** - Monitor website availability
6. **Network Devices Tab** - Scan and view devices on your network
7. **Settings** - Configure monitoring parameters and language

### Configuration

Access settings by clicking the **Settings** button. You can configure:

- **Ping Settings**
  - Primary and secondary ping targets
  - Ping interval (1-60 seconds)
  - Timeout duration

- **Notification Thresholds**
  - High ping threshold
  - Packet loss threshold
  - Downtime threshold

- **Application Settings**
  - Enable/disable notifications
  - Minimize to tray behavior
  - Start with Windows
  - Data retention period
  - Language selection

## Tech Stack

- **Framework:** .NET 8.0 WPF
- **Architecture:** MVVM Pattern
- **UI Toolkit:** CommunityToolkit.Mvvm
- **Charts:** LiveCharts2 (SkiaSharp)
- **Database:** SQLite (Microsoft.Data.Sqlite)
- **Logging:** Custom file-based logger

## Project Structure

```
InternetMonitor/
├── Core/
│   ├── Interfaces/          # Service interfaces
│   ├── Models/              # Data models
│   └── Services/            # Core business logic
├── Infrastructure/
│   ├── Configuration/       # App settings
│   ├── Helpers/             # Utility classes
│   ├── Localization/        # Multi-language support
│   ├── Logging/             # Logging service
│   └── Notifications/       # Desktop notifications
├── UI/
│   ├── Controls/            # Custom controls
│   ├── Converters/          # Value converters
│   ├── ViewModels/          # MVVM ViewModels
│   └── Views/               # XAML views
├── App.xaml                 # Application entry
├── MainWindow.xaml          # Main window
└── InternetMonitor.csproj   # Project file
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

**Kemal Acar**
- Website: [kemalacar.com](https://kemalacar.com)
- Email: kemalacarofficial@gmail.com
- GitHub: [@kemalai](https://github.com/kemalai)

## Acknowledgments

- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2) - Beautiful charts library
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit
- [SkiaSharp](https://github.com/mono/SkiaSharp) - Cross-platform 2D graphics

---

Made with passion in Turkey
