using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Viture.Deployer.Editor
{
    /// <summary>
    /// Unity Editor window for building, connecting, and deploying to Viture devices via ADB.
    /// </summary>
    public class VitureDeployWindow : EditorWindow
    {
        // Connection settings
        private string _pairIp = "192.168.";
        private string _pairPort = "";
        private string _pairCode = "";
        private string _connectIp = "192.168.";
        private string _connectPort = "5555";
        
        // State
        private string _connectedDeviceSerial;
        private string _lastBuiltApkPath = "";
        private bool _isConnected;
        private bool _isProcessing;
        private string _statusMessage = "Not connected";
        private Color _statusColor = Color.gray;
        
        // Discovered devices
        private List<DiscoveredDevice> _discoveredDevices = new List<DiscoveredDevice>();
        private int _selectedDeviceIndex = -1;
        
        // Installed apps
        private List<AppInfo> _installedApps = new List<AppInfo>();
        private int _selectedAppIndex = -1;
        private bool _showAllApps;
        
        // Log
        private List<string> _logMessages = new List<string>();
        private Vector2 _logScrollPosition;
        private Vector2 _deviceScrollPosition;
        private Vector2 _appScrollPosition;
        
        // Settings
        private VitureDeployerSettings _settings;
        
        // UI State
        private int _selectedTab;
        private readonly string[] _tabNames = { "Connect", "Deploy", "Apps" };

        public static void ShowWindow()
        {
            var window = GetWindow<VitureDeployWindow>("Viture Deploy");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            CheckAdbAvailable();
            _ = ScanForDevicesAsync();
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            _settings = VitureDeployerSettings.Load();
            _pairIp = _settings.PairIp;
            _pairPort = _settings.PairPort;
            _connectIp = _settings.ConnectIp;
            _connectPort = _settings.ConnectPort;
            _showAllApps = _settings.ShowAllApps;
            _lastBuiltApkPath = _settings.LastApkPath;

            // Load saved devices
            foreach (var device in _settings.SavedDevices.OrderByDescending(d => d.LastConnected))
            {
                _discoveredDevices.Add(new DiscoveredDevice
                {
                    Name = $"{device.Name} (saved)",
                    Ip = device.Ip,
                    Port = device.Port,
                    IsSaved = true
                });
            }
        }

        private void SaveSettings()
        {
            if (_settings == null) return;
            
            _settings.PairIp = _pairIp;
            _settings.PairPort = _pairPort;
            _settings.ConnectIp = _connectIp;
            _settings.ConnectPort = _connectPort;
            _settings.ShowAllApps = _showAllApps;
            _settings.LastApkPath = _lastBuiltApkPath;
            _settings.Save();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // Header with status
            DrawHeader();
            
            EditorGUILayout.Space(5);
            
            // Tab bar
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            
            EditorGUILayout.Space(10);
            
            // Tab content
            switch (_selectedTab)
            {
                case 0:
                    DrawConnectTab();
                    break;
                case 1:
                    DrawDeployTab();
                    break;
                case 2:
                    DrawAppsTab();
                    break;
            }
            
            EditorGUILayout.Space(10);
            
            // Log section
            DrawLogSection();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Status indicator
            var oldColor = GUI.color;
            GUI.color = _statusColor;
            GUILayout.Label("●", GUILayout.Width(20));
            GUI.color = oldColor;
            
            GUILayout.Label(_statusMessage, EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (_isConnected)
            {
                if (GUILayout.Button("Disconnect", EditorStyles.toolbarButton))
                {
                    _ = DisconnectAsync();
                }
            }
            
            if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                ShowHelp();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnectTab()
        {
            // Device Discovery Section
            EditorGUILayout.LabelField("Discovered Devices", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_isProcessing;
            if (GUILayout.Button("🔍 Scan", GUILayout.Width(80)))
            {
                _ = ScanForDevicesAsync();
            }
            GUI.enabled = true;
            
            if (_discoveredDevices.Count > 0)
            {
                GUILayout.Label($"Found {_discoveredDevices.Count} device(s)");
            }
            EditorGUILayout.EndHorizontal();
            
            // Device list
            _deviceScrollPosition = EditorGUILayout.BeginScrollView(_deviceScrollPosition, GUILayout.Height(100));
            for (int i = 0; i < _discoveredDevices.Count; i++)
            {
                var device = _discoveredDevices[i];
                EditorGUILayout.BeginHorizontal(i == _selectedDeviceIndex ? "SelectionRect" : "box");
                
                if (GUILayout.Button(device.Name, EditorStyles.label))
                {
                    _selectedDeviceIndex = i;
                    _connectIp = device.Ip;
                    _connectPort = device.Port;
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{device.Ip}:{device.Port}", EditorStyles.miniLabel);
                
                GUI.enabled = !_isProcessing;
                if (GUILayout.Button("Connect", GUILayout.Width(60)))
                {
                    _ = QuickConnectAsync(device);
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // Manual Connection Section
            EditorGUILayout.LabelField("Manual Connection", EditorStyles.boldLabel);
            
            // Pairing (first-time setup)
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("First-Time Pairing", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("IP:", GUILayout.Width(60));
            _pairIp = EditorGUILayout.TextField(_pairIp);
            EditorGUILayout.LabelField("Port:", GUILayout.Width(35));
            _pairPort = EditorGUILayout.TextField(_pairPort, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pairing Code:", GUILayout.Width(85));
            _pairCode = EditorGUILayout.TextField(_pairCode);
            
            GUI.enabled = !_isProcessing && !string.IsNullOrEmpty(_pairIp) && !string.IsNullOrEmpty(_pairPort) && !string.IsNullOrEmpty(_pairCode);
            if (GUILayout.Button("Pair", GUILayout.Width(50)))
            {
                _ = PairAsync();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Connect
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Connect", EditorStyles.miniBoldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("IP:", GUILayout.Width(60));
            _connectIp = EditorGUILayout.TextField(_connectIp);
            EditorGUILayout.LabelField("Port:", GUILayout.Width(35));
            _connectPort = EditorGUILayout.TextField(_connectPort, GUILayout.Width(60));
            
            GUI.enabled = !_isProcessing && !string.IsNullOrEmpty(_connectIp) && !string.IsNullOrEmpty(_connectPort);
            if (GUILayout.Button("Connect", GUILayout.Width(60)))
            {
                _ = ConnectAsync();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawDeployTab()
        {
            // Build Section
            EditorGUILayout.LabelField("Build & Deploy", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // Current build target info
            var currentTarget = EditorUserBuildSettings.activeBuildTarget;
            var isAndroid = currentTarget == BuildTarget.Android;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Build Target:", GUILayout.Width(80));
            
            var oldColor = GUI.color;
            GUI.color = isAndroid ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(currentTarget.ToString(), EditorStyles.boldLabel);
            GUI.color = oldColor;
            
            if (!isAndroid)
            {
                if (GUILayout.Button("Switch to Android", GUILayout.Width(120)))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Build buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !_isProcessing && isAndroid;
            if (GUILayout.Button("Build APK", GUILayout.Height(30)))
            {
                BuildApk();
            }
            
            GUI.enabled = !_isProcessing && isAndroid && _isConnected;
            if (GUILayout.Button("Build & Deploy", GUILayout.Height(30)))
            {
                BuildAndDeploy();
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Manual APK Install Section
            EditorGUILayout.LabelField("Install APK", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("APK:", GUILayout.Width(35));
            
            var displayPath = string.IsNullOrEmpty(_lastBuiltApkPath) ? "No APK selected..." : Path.GetFileName(_lastBuiltApkPath);
            EditorGUILayout.LabelField(displayPath, EditorStyles.textField);
            
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                var path = EditorUtility.OpenFilePanel("Select APK", "", "apk");
                if (!string.IsNullOrEmpty(path))
                {
                    _lastBuiltApkPath = path;
                    Log($"Selected: {path}");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = !_isProcessing && _isConnected && !string.IsNullOrEmpty(_lastBuiltApkPath) && File.Exists(_lastBuiltApkPath);
            if (GUILayout.Button("Install APK"))
            {
                _ = InstallApkAsync(_lastBuiltApkPath);
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndVertical();
            
            // Last built APK info
            if (!string.IsNullOrEmpty(_lastBuiltApkPath) && File.Exists(_lastBuiltApkPath))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Last APK: {_lastBuiltApkPath}", MessageType.Info);
            }
        }

        private void DrawAppsTab()
        {
            EditorGUILayout.LabelField("Installed Apps", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !_isProcessing && _isConnected;
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                _ = RefreshAppListAsync();
            }
            GUI.enabled = true;
            
            var newShowAll = EditorGUILayout.ToggleLeft("Show All Apps", _showAllApps);
            if (newShowAll != _showAllApps)
            {
                _showAllApps = newShowAll;
                FilterApps();
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_installedApps.Count} apps");
            
            EditorGUILayout.EndHorizontal();
            
            // App list
            _appScrollPosition = EditorGUILayout.BeginScrollView(_appScrollPosition);
            
            for (int i = 0; i < _installedApps.Count; i++)
            {
                var app = _installedApps[i];
                EditorGUILayout.BeginHorizontal(i == _selectedAppIndex ? "SelectionRect" : "box");
                
                // Unity indicator
                if (app.IsUnityApp)
                {
                    GUILayout.Label("🎮", GUILayout.Width(20));
                }
                else
                {
                    GUILayout.Label("  ", GUILayout.Width(20));
                }
                
                if (GUILayout.Button(new GUIContent(app.DisplayName, app.PackageName), EditorStyles.label))
                {
                    _selectedAppIndex = i;
                }
                
                GUILayout.FlexibleSpace();
                
                GUI.enabled = !_isProcessing && _isConnected;
                if (GUILayout.Button("Uninstall", GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Confirm Uninstall", 
                        $"Are you sure you want to uninstall:\n\n{app.DisplayName}\n({app.PackageName})?", 
                        "Yes", "No"))
                    {
                        _ = UninstallAppAsync(app);
                    }
                }
                GUI.enabled = true;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _logMessages.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(120));
            
            foreach (var msg in _logMessages)
            {
                EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
            }
            
            EditorGUILayout.EndScrollView();
        }

        #region ADB Operations

        private async void CheckAdbAvailable()
        {
            var result = await AdbHelper.RunCommandAsync("version");
            if (result.Success)
            {
                var version = result.Output.Split('\n').FirstOrDefault();
                Log($"✓ ADB found: {version}");
            }
            else
            {
                Log("⚠ ADB not found in PATH. Please install Android Platform Tools.");
                Log("  Download from: https://developer.android.com/tools/releases/platform-tools");
                SetStatus("ADB not found", Color.red);
            }
            Repaint();
        }

        private async Task ScanForDevicesAsync()
        {
            _isProcessing = true;
            SetStatus("Scanning...", Color.yellow);
            Repaint();

            try
            {
                // Keep saved devices
                var savedDevices = _discoveredDevices.Where(d => d.IsSaved).ToList();
                _discoveredDevices.Clear();
                _discoveredDevices.AddRange(savedDevices);

                // Check for already connected ADB devices
                var result = await AdbHelper.RunCommandAsync("devices");
                if (result.Success)
                {
                    var lines = result.Output.Split('\n')
                        .Skip(1)
                        .Where(l => l.Contains("device") && !l.Contains("offline"));

                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var serial = parts.FirstOrDefault()?.Trim();

                        if (string.IsNullOrEmpty(serial)) continue;

                        if (serial.Contains(':'))
                        {
                            var ipParts = serial.Split(':');
                            var ip = ipParts[0];
                            var port = ipParts.Length > 1 ? ipParts[1] : "5555";

                            if (!_discoveredDevices.Any(d => d.Ip == ip && d.Port == port))
                            {
                                _discoveredDevices.Add(new DiscoveredDevice
                                {
                                    Name = "Connected Device",
                                    Ip = ip,
                                    Port = port,
                                    IsConnected = true
                                });
                            }
                        }
                    }
                }

                SetStatus(_discoveredDevices.Count > 0 
                    ? $"Found {_discoveredDevices.Count} device(s)" 
                    : "No devices found", Color.gray);
            }
            catch (Exception ex)
            {
                Log($"⚠ Scan error: {ex.Message}");
                SetStatus("Scan failed", Color.red);
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }

        private async Task PairAsync()
        {
            _isProcessing = true;
            Log($"Pairing with {_pairIp}:{_pairPort}...");
            SetStatus("Pairing...", Color.yellow);
            Repaint();

            var result = await AdbHelper.RunCommandAsync($"pair {_pairIp}:{_pairPort} {_pairCode}");

            if (result.Success && result.Output.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase))
            {
                Log($"✓ {result.Output}");
                SetStatus("Paired successfully", Color.green);
                _connectIp = _pairIp;
            }
            else
            {
                Log($"✗ Pairing failed: {result.Output}");
                SetStatus("Pairing failed", Color.red);
            }

            _isProcessing = false;
            Repaint();
        }

        private async Task ConnectAsync()
        {
            _isProcessing = true;
            Log($"Connecting to {_connectIp}:{_connectPort}...");
            SetStatus("Connecting...", Color.yellow);
            Repaint();

            var result = await AdbHelper.RunCommandAsync($"connect {_connectIp}:{_connectPort}");

            if (result.Success && (result.Output.Contains("connected", StringComparison.OrdinalIgnoreCase) ||
                                   result.Output.Contains("already connected", StringComparison.OrdinalIgnoreCase)))
            {
                _connectedDeviceSerial = $"{_connectIp}:{_connectPort}";
                _isConnected = true;
                Log($"✓ {result.Output}");
                SetStatus("Connected", Color.green);

                SaveDeviceToSettings(new DiscoveredDevice { Name = "Device", Ip = _connectIp, Port = _connectPort });
            }
            else
            {
                Log($"✗ Connection failed: {result.Output}");
                SetStatus("Connection failed", Color.red);
            }

            _isProcessing = false;
            Repaint();
        }

        private async Task QuickConnectAsync(DiscoveredDevice device)
        {
            _connectIp = device.Ip;
            _connectPort = device.Port;
            await ConnectAsync();
        }

        private async Task DisconnectAsync()
        {
            if (string.IsNullOrEmpty(_connectedDeviceSerial)) return;

            _isProcessing = true;
            Log($"Disconnecting from {_connectedDeviceSerial}...");
            Repaint();

            await AdbHelper.RunCommandAsync($"disconnect {_connectedDeviceSerial}");

            _connectedDeviceSerial = null;
            _isConnected = false;
            _installedApps.Clear();

            Log("✓ Disconnected");
            SetStatus("Disconnected", Color.gray);

            _isProcessing = false;
            await ScanForDevicesAsync();
        }

        private async Task InstallApkAsync(string apkPath)
        {
            _isProcessing = true;
            Log($"Installing {Path.GetFileName(apkPath)}...");
            Log("This may take a minute...");
            SetStatus("Installing...", Color.yellow);
            Repaint();

            var result = await AdbHelper.RunCommandAsync($"-s {_connectedDeviceSerial} install -r -d -g \"{apkPath}\"");

            if (result.Success && result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                Log("✓ APK installed successfully!");
                SetStatus("Installed!", Color.green);
                EditorUtility.DisplayDialog("Success", "APK installed successfully!", "OK");
            }
            else
            {
                Log($"✗ Installation failed: {result.Output}");
                SetStatus("Installation failed", Color.red);
            }

            _isProcessing = false;
            Repaint();
        }

        private async Task RefreshAppListAsync()
        {
            _isProcessing = true;
            Log("Fetching installed apps...");
            SetStatus("Loading apps...", Color.yellow);
            Repaint();

            var result = await AdbHelper.RunCommandAsync($"-s {_connectedDeviceSerial} shell pm list packages -3");

            if (!result.Success)
            {
                Log($"✗ Failed to get app list: {result.Output}");
                SetStatus("Failed to get apps", Color.red);
                _isProcessing = false;
                Repaint();
                return;
            }

            _allApps.Clear();

            var lines = result.Output.Split('\n');
            var packageNames = lines
                .Select(line => line.Replace("package:", "").Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            foreach (var packageName in packageNames)
            {
                _allApps.Add(new AppInfo
                {
                    PackageName = packageName,
                    DisplayName = GetDisplayName(packageName),
                    IsUnityApp = IsLikelyUnityApp(packageName)
                });
            }

            FilterApps();

            Log($"✓ Found {_allApps.Count} third-party apps");
            SetStatus("Connected", Color.green);
            _isProcessing = false;
            Repaint();
        }

        private List<AppInfo> _allApps = new List<AppInfo>();

        private void FilterApps()
        {
            _installedApps.Clear();
            var appsToShow = _showAllApps ? _allApps : _allApps.Where(a => a.IsUnityApp).ToList();
            _installedApps.AddRange(appsToShow.OrderBy(a => a.DisplayName));
            _selectedAppIndex = -1;
        }

        private async Task UninstallAppAsync(AppInfo app)
        {
            _isProcessing = true;
            Log($"Uninstalling {app.PackageName}...");
            SetStatus("Uninstalling...", Color.yellow);
            Repaint();

            var result = await AdbHelper.RunCommandAsync($"-s {_connectedDeviceSerial} uninstall {app.PackageName}");

            if (result.Success && result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                Log($"✓ {app.DisplayName} uninstalled successfully!");
                SetStatus("Uninstalled!", Color.green);

                _allApps.RemoveAll(a => a.PackageName == app.PackageName);
                FilterApps();
            }
            else
            {
                Log($"✗ Uninstall failed: {result.Output}");
                SetStatus("Uninstall failed", Color.red);
            }

            _isProcessing = false;
            Repaint();
        }

        #endregion

        #region Build Operations

        private void BuildApk()
        {
            var apkPath = GetBuildPath();
            
            Log($"Building APK to: {apkPath}");
            SetStatus("Building...", Color.yellow);
            
            var report = BuildPipeline.BuildPlayer(GetBuildPlayerOptions(apkPath));
            
            if (report.summary.result == BuildResult.Succeeded)
            {
                _lastBuiltApkPath = apkPath;
                Log($"✓ Build succeeded: {apkPath}");
                SetStatus("Build succeeded", Color.green);
            }
            else
            {
                Log($"✗ Build failed: {report.summary.totalErrors} errors");
                SetStatus("Build failed", Color.red);
            }
            
            Repaint();
        }

        private void BuildAndDeploy()
        {
            var apkPath = GetBuildPath();
            
            Log($"Building APK to: {apkPath}");
            SetStatus("Building...", Color.yellow);
            Repaint();
            
            var report = BuildPipeline.BuildPlayer(GetBuildPlayerOptions(apkPath));
            
            if (report.summary.result == BuildResult.Succeeded)
            {
                _lastBuiltApkPath = apkPath;
                Log($"✓ Build succeeded, deploying...");
                _ = InstallApkAsync(apkPath);
            }
            else
            {
                Log($"✗ Build failed: {report.summary.totalErrors} errors");
                SetStatus("Build failed", Color.red);
                Repaint();
            }
        }

        private string GetBuildPath()
        {
            var buildFolder = Path.Combine(Application.dataPath, "..", "Builds", "Android");
            Directory.CreateDirectory(buildFolder);
            return Path.Combine(buildFolder, $"{Application.productName}.apk");
        }

        private BuildPlayerOptions GetBuildPlayerOptions(string locationPathName)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            return new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPathName,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
        }

        #endregion

        #region Helpers

        private void SaveDeviceToSettings(DiscoveredDevice device)
        {
            var existing = _settings.SavedDevices.FirstOrDefault(d => d.Ip == device.Ip);
            if (existing != null)
            {
                existing.LastConnected = DateTime.Now.ToString("o");
                existing.Port = device.Port;
            }
            else
            {
                _settings.SavedDevices.Add(new SavedDevice
                {
                    Name = device.Name.Replace(" (saved)", ""),
                    Ip = device.Ip,
                    Port = device.Port,
                    LastConnected = DateTime.Now.ToString("o")
                });
            }
            _settings.Save();
        }

        private static bool IsLikelyUnityApp(string packageName)
        {
            var lowerName = packageName.ToLowerInvariant();

            if (lowerName.Contains("unity")) return true;
            if (lowerName.Contains("com.defaultcompany")) return true;

            var unityPatterns = new[]
            {
                "viture", "spacewalker", "xr", "vr", "ar",
                "oculus", "meta", "immersive", "spatial"
            };

            return unityPatterns.Any(p => lowerName.Contains(p));
        }

        private static string GetDisplayName(string packageName)
        {
            var parts = packageName.Split('.');
            if (parts.Length >= 2)
            {
                var name = parts[parts.Length - 1];
                var result = string.Concat(name.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
                return char.ToUpper(result[0]) + result.Substring(1);
            }
            return packageName;
        }

        private void Log(string message)
        {
            _logMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            
            // Keep log size reasonable
            while (_logMessages.Count > 100)
            {
                _logMessages.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            _logScrollPosition.y = float.MaxValue;
        }

        private void SetStatus(string message, Color color)
        {
            _statusMessage = message;
            _statusColor = color;
        }

        private void ShowHelp()
        {
            EditorUtility.DisplayDialog("Connection Help",
                @"How to Connect Your VITURE Neckband:

1️⃣ ENABLE WIRELESS DEBUGGING
   • On your neckband, go to Settings → Developer Options
   • Enable 'Wireless debugging'
   • Note the IP address and Port shown

2️⃣ FIRST TIME PAIRING
   • Tap 'Pair device with pairing code'
   • Enter the IP, Pairing Port, and 6-digit Code
   • Click 'Pair' in this window

3️⃣ CONNECT
   • Enter the IP and Port (usually 5555)
   • Click 'Connect'

💡 TIPS:
   • The pairing port is different from the connect port
   • After pairing once, you only need to connect
   • Saved devices appear in the list automatically
   • Click Scan to find devices on your network", "OK");
        }

        #endregion
    }

    [Serializable]
    public class DiscoveredDevice
    {
        public string Name = "";
        public string Ip = "";
        public string Port = "5555";
        public bool IsViture;
        public bool IsSaved;
        public bool IsConnected;
    }

    [Serializable]
    public class AppInfo
    {
        public string PackageName = "";
        public string DisplayName = "";
        public bool IsUnityApp;
    }
}
