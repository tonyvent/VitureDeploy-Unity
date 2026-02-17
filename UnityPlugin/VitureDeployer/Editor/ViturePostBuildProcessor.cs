using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Viture.Deployer.Editor
{
    /// <summary>
    /// Post-build processor that can automatically deploy APKs to connected Viture devices.
    /// </summary>
    public class ViturePostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            // Only process Android builds
            if (report.summary.platform != BuildTarget.Android)
                return;

            // Only process successful builds
            if (report.summary.result != BuildResult.Succeeded)
                return;

            var settings = VitureDeployerSettings.Load();

            // Check if auto-deploy is enabled
            if (!settings.AutoDeployAfterBuild)
                return;

            // Check if we have a last-known connected device
            if (settings.SavedDevices.Count == 0)
            {
                Debug.Log("[VitureDeployer] Auto-deploy skipped: No saved devices. Connect a device first via Tools > Viture > Deploy Window");
                return;
            }

            var apkPath = report.summary.outputPath;
            Debug.Log($"[VitureDeployer] Build completed: {apkPath}");
            
            // Store the APK path for easy access
            settings.LastApkPath = apkPath;
            settings.Save();

            // Try to deploy to the most recently connected device
            var mostRecentDevice = GetMostRecentDevice(settings);
            if (mostRecentDevice != null)
            {
                Debug.Log($"[VitureDeployer] Auto-deploying to {mostRecentDevice.Name} ({mostRecentDevice.Ip}:{mostRecentDevice.Port})...");
                DeployToDeviceAsync(mostRecentDevice, apkPath);
            }
        }

        private SavedDevice GetMostRecentDevice(VitureDeployerSettings settings)
        {
            SavedDevice mostRecent = null;
            foreach (var device in settings.SavedDevices)
            {
                if (mostRecent == null || device.LastConnectedDate > mostRecent.LastConnectedDate)
                {
                    mostRecent = device;
                }
            }
            return mostRecent;
        }

        private async void DeployToDeviceAsync(SavedDevice device, string apkPath)
        {
            var deviceSerial = $"{device.Ip}:{device.Port}";

            // First, try to connect
            var connectResult = await AdbHelper.ConnectAsync(device.Ip, device.Port);
            if (!connectResult.Success && !connectResult.Output.Contains("already connected"))
            {
                Debug.LogWarning($"[VitureDeployer] Could not connect to {deviceSerial}: {connectResult.Output}");
                return;
            }

            // Install the APK
            var installResult = await AdbHelper.InstallAsync(deviceSerial, apkPath);
            
            if (installResult.Success && installResult.Output.Contains("Success"))
            {
                Debug.Log($"[VitureDeployer] ✓ APK deployed successfully to {device.Name}!");
                
                // Optionally launch the app
                var packageName = PlayerSettings.applicationIdentifier;
                if (!string.IsNullOrEmpty(packageName))
                {
                    var launchResult = await AdbHelper.LaunchAppAsync(deviceSerial, packageName);
                    if (launchResult.Success)
                    {
                        Debug.Log($"[VitureDeployer] ✓ Launched {packageName}");
                    }
                }
            }
            else
            {
                Debug.LogError($"[VitureDeployer] ✗ Failed to deploy APK: {installResult.Output}");
            }
        }
    }
}
