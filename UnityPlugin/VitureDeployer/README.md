# Viture Deployer - Unity Editor Plugin

**Created by [BananaRhinoStudios](https://github.com/tonyvent/VitureDeploy-Unity)**

A Unity Editor plugin for building, connecting, and deploying APKs to Viture Neckband devices (and other Android devices) via wireless ADB.

## Features

- 🔍 **Device Discovery** - Scan for and connect to devices on your network
- 🔗 **Wireless ADB** - Pair and connect without USB cables
- 🔨 **One-Click Build & Deploy** - Build your Unity project and deploy directly to device
- 📱 **App Management** - View and uninstall installed apps
- ⚡ **Auto-Deploy** - Optionally auto-deploy after every build
- 💾 **Saved Devices** - Remember devices for quick reconnection

## Installation

### Option 1: Copy to Assets folder

1. Copy the entire `VitureDeployer` folder into your Unity project's `Assets` folder
2. The plugin will appear under **Tools > Viture** in the Unity menu

### Option 2: Unity Package

1. In Unity, go to **Assets > Import Package > Custom Package**
2. Select the `VitureDeployer.unitypackage` file
3. Click **Import**

## Prerequisites

- **Android SDK Platform Tools** - ADB must be installed and in your system PATH
  - Download from: https://developer.android.com/tools/releases/platform-tools
  - Or use the Android SDK installed by Unity (Preferences > External Tools)

## Quick Start

### 1. Open the Deploy Window

Go to **Tools > Viture > Deploy Window**

### 2. Connect Your Device

#### First Time Setup (Pairing):
1. On your Viture Neckband, go to **Settings > Developer Options**
2. Enable **Wireless debugging**
3. Tap **Pair device with pairing code**
4. Note the IP, Pairing Port, and 6-digit code
5. Enter these in the "First-Time Pairing" section
6. Click **Pair**

#### Connecting (After Pairing):
1. Enter the device IP and Port (usually 5555)
2. Click **Connect**
3. Or click **Scan** to find devices automatically

### 3. Build & Deploy

1. Switch to the **Deploy** tab
2. Ensure your build target is **Android**
3. Click **Build & Deploy** to build your project and install it on the device

## Menu Options

| Menu Item | Description |
|-----------|-------------|
| **Deploy Window** | Opens the main deployment window |
| **Quick Deploy Last APK** | Instantly deploys the last built APK to the most recent device |
| **Build and Deploy** | Opens the deploy window for building |
| **Toggle Auto-Deploy** | Enables/disables automatic deployment after builds |
| **Open Android SDK Location** | Opens the Android SDK folder in file explorer |

## Keyboard Shortcuts

You can add keyboard shortcuts via Unity's Shortcut Manager (**Edit > Shortcuts**):
- Search for "Viture" to find available commands

## Tabs

### Connect Tab
- Scan for devices on your network
- Pair new devices with pairing codes
- Connect to known devices
- Saved devices appear automatically

### Deploy Tab
- Build your Unity project to APK
- One-click build and deploy
- Browse and install existing APK files
- View current build target status

### Apps Tab
- View installed third-party apps
- Filter to show only Unity/XR apps
- Uninstall apps directly from Unity

## Auto-Deploy

When enabled (**Tools > Viture > Toggle Auto-Deploy**), the plugin will automatically:
1. Detect when a build completes
2. Connect to your most recently used device
3. Install the APK
4. Launch the app

## Troubleshooting

### "ADB not found"
- Ensure Android Platform Tools are installed
- Add the `platform-tools` folder to your system PATH
- Or configure the Android SDK path in Unity Preferences > External Tools

### "Connection failed"
- Ensure wireless debugging is enabled on the device
- Check that both devices are on the same network
- Try pairing again if the device was restarted

### "Multiple devices" error
- The plugin automatically handles multiple devices by targeting specific serials
- If issues persist, disconnect all devices and reconnect to just one

## File Structure

```
VitureDeployer/
└── Editor/
    ├── Viture.Deployer.Editor.asmdef  # Assembly definition
    ├── VitureDeployWindow.cs          # Main editor window
    ├── AdbHelper.cs                   # ADB command execution
    ├── VitureDeployerSettings.cs      # Persistent settings
    ├── ViturePostBuildProcessor.cs    # Auto-deploy on build
    └── VitureMenuItems.cs             # Menu shortcuts
```

## API Usage

You can also use the ADB helper in your own editor scripts:

```csharp
using Viture.Deployer.Editor;

// Run any ADB command
var result = await AdbHelper.RunCommandAsync("devices");

// Install an APK
var result = await AdbHelper.InstallAsync("192.168.1.100:5555", "/path/to/app.apk");

// Launch an app
var result = await AdbHelper.LaunchAppAsync("192.168.1.100:5555", "com.company.app");
```
## Demo

https://github.com/user-attachments/assets/4d2a13ba-b3c4-4f8b-936d-08360f1ca8d5