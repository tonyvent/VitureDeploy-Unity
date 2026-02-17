using UnityEditor;
using UnityEngine;

namespace Viture.Deployer.Editor
{
    /// <summary>
    /// Provides menu shortcuts for common Viture Deployer actions.
    /// </summary>
    public static class VitureMenuItems
    {
        private const string MenuRoot = "Tools/Viture/";

        [MenuItem(MenuRoot + "Deploy Window", false, 0)]
        public static void OpenDeployWindow()
        {
            VitureDeployWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Quick Deploy Last APK", false, 20)]
        public static async void QuickDeployLastApk()
        {
            var settings = VitureDeployerSettings.Load();

            if (string.IsNullOrEmpty(settings.LastApkPath) || !System.IO.File.Exists(settings.LastApkPath))
            {
                EditorUtility.DisplayDialog("No APK", 
                    "No APK found. Please build your project first or select an APK in the Deploy Window.", 
                    "OK");
                return;
            }

            if (settings.SavedDevices.Count == 0)
            {
                EditorUtility.DisplayDialog("No Device", 
                    "No saved devices found. Please connect to a device first via Tools > Viture > Deploy Window.", 
                    "OK");
                return;
            }

            // Get most recent device
            SavedDevice mostRecent = null;
            foreach (var device in settings.SavedDevices)
            {
                if (mostRecent == null || device.LastConnectedDate > mostRecent.LastConnectedDate)
                {
                    mostRecent = device;
                }
            }

            if (mostRecent == null)
            {
                EditorUtility.DisplayDialog("No Device", "No device available for deployment.", "OK");
                return;
            }

            var deviceSerial = $"{mostRecent.Ip}:{mostRecent.Port}";

            Debug.Log($"[VitureDeployer] Connecting to {deviceSerial}...");
            var connectResult = await AdbHelper.ConnectAsync(mostRecent.Ip, mostRecent.Port);

            if (!connectResult.Success && !connectResult.Output.Contains("already connected"))
            {
                EditorUtility.DisplayDialog("Connection Failed", 
                    $"Could not connect to {deviceSerial}:\n{connectResult.Output}", 
                    "OK");
                return;
            }

            Debug.Log($"[VitureDeployer] Installing {settings.LastApkPath}...");
            var installResult = await AdbHelper.InstallAsync(deviceSerial, settings.LastApkPath);

            if (installResult.Success && installResult.Output.Contains("Success"))
            {
                Debug.Log("[VitureDeployer] ✓ APK installed successfully!");
                EditorUtility.DisplayDialog("Success", "APK installed successfully!", "OK");
            }
            else
            {
                Debug.LogError($"[VitureDeployer] ✗ Installation failed: {installResult.Output}");
                EditorUtility.DisplayDialog("Installation Failed", installResult.Output, "OK");
            }
        }

        [MenuItem(MenuRoot + "Quick Deploy Last APK", true)]
        public static bool QuickDeployLastApkValidate()
        {
            var settings = VitureDeployerSettings.Load();
            return !string.IsNullOrEmpty(settings.LastApkPath) && 
                   System.IO.File.Exists(settings.LastApkPath) &&
                   settings.SavedDevices.Count > 0;
        }

        [MenuItem(MenuRoot + "Build and Deploy", false, 21)]
        public static void BuildAndDeploy()
        {
            var window = EditorWindow.GetWindow<VitureDeployWindow>("Viture Deploy");
            window.Show();
            // The window will handle the build and deploy
        }

        [MenuItem(MenuRoot + "Toggle Auto-Deploy", false, 40)]
        public static void ToggleAutoDeploy()
        {
            var settings = VitureDeployerSettings.Load();
            settings.AutoDeployAfterBuild = !settings.AutoDeployAfterBuild;
            settings.Save();

            var status = settings.AutoDeployAfterBuild ? "enabled" : "disabled";
            Debug.Log($"[VitureDeployer] Auto-deploy after build: {status}");
        }

        [MenuItem(MenuRoot + "Toggle Auto-Deploy", true)]
        public static bool ToggleAutoDeployValidate()
        {
            var settings = VitureDeployerSettings.Load();
            Menu.SetChecked(MenuRoot + "Toggle Auto-Deploy", settings.AutoDeployAfterBuild);
            return true;
        }

        [MenuItem(MenuRoot + "Open Android SDK Location", false, 60)]
        public static void OpenAndroidSdkLocation()
        {
            var sdkPath = EditorPrefs.GetString("AndroidSdkRoot");
            if (string.IsNullOrEmpty(sdkPath))
            {
                // Try environment variable
                sdkPath = System.Environment.GetEnvironmentVariable("ANDROID_HOME") ??
                          System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            }

            if (!string.IsNullOrEmpty(sdkPath) && System.IO.Directory.Exists(sdkPath))
            {
                EditorUtility.RevealInFinder(sdkPath);
            }
            else
            {
                EditorUtility.DisplayDialog("SDK Not Found", 
                    "Android SDK location not found. Please configure it in Unity Preferences > External Tools.", 
                    "OK");
            }
        }

        [MenuItem(MenuRoot + "Documentation", false, 80)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/tonyvent/VitureDeploy-Unity");
        }
    }
}
