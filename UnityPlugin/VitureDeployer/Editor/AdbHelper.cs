using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Viture.Deployer.Editor
{
    /// <summary>
    /// Helper class for running ADB commands asynchronously.
    /// </summary>
    public static class AdbHelper
    {
        private static string _adbPath = "adb"; // Assumes adb is in PATH

        /// <summary>
        /// Sets a custom ADB path. Useful if ADB is not in the system PATH.
        /// </summary>
        public static void SetAdbPath(string path)
        {
            _adbPath = path;
        }

        /// <summary>
        /// Runs an ADB command asynchronously and returns the result.
        /// </summary>
        /// <param name="arguments">ADB command arguments (e.g., "devices", "install myapp.apk")</param>
        /// <returns>AdbResult containing success status and output</returns>
        public static Task<AdbResult> RunCommandAsync(string arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _adbPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        return new AdbResult(false, "Failed to start ADB process");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    var combinedOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
                    return new AdbResult(process.ExitCode == 0, combinedOutput.Trim());
                }
                catch (Exception ex)
                {
                    return new AdbResult(false, $"Error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Runs an ADB pair command.
        /// </summary>
        public static Task<AdbResult> PairAsync(string ip, string port, string pairingCode)
        {
            return RunCommandAsync($"pair {ip}:{port} {pairingCode}");
        }

        /// <summary>
        /// Connects to a device via wireless ADB.
        /// </summary>
        public static Task<AdbResult> ConnectAsync(string ip, string port)
        {
            return RunCommandAsync($"connect {ip}:{port}");
        }

        /// <summary>
        /// Disconnects from a device.
        /// </summary>
        public static Task<AdbResult> DisconnectAsync(string deviceSerial)
        {
            return RunCommandAsync($"disconnect {deviceSerial}");
        }

        /// <summary>
        /// Installs an APK to the connected device.
        /// </summary>
        /// <param name="deviceSerial">Device serial/IP:port to target</param>
        /// <param name="apkPath">Full path to the APK file</param>
        /// <param name="allowDowngrade">Allow installing older versions</param>
        /// <param name="grantPermissions">Automatically grant all runtime permissions</param>
        public static Task<AdbResult> InstallAsync(string deviceSerial, string apkPath, bool allowDowngrade = true, bool grantPermissions = true)
        {
            var flags = "-r"; // Replace existing
            if (allowDowngrade) flags += " -d";
            if (grantPermissions) flags += " -g";

            return RunCommandAsync($"-s {deviceSerial} install {flags} \"{apkPath}\"");
        }

        /// <summary>
        /// Uninstalls a package from the connected device.
        /// </summary>
        public static Task<AdbResult> UninstallAsync(string deviceSerial, string packageName)
        {
            return RunCommandAsync($"-s {deviceSerial} uninstall {packageName}");
        }

        /// <summary>
        /// Lists installed third-party packages on the device.
        /// </summary>
        public static Task<AdbResult> ListPackagesAsync(string deviceSerial, bool thirdPartyOnly = true)
        {
            var flags = thirdPartyOnly ? "-3" : "";
            return RunCommandAsync($"-s {deviceSerial} shell pm list packages {flags}");
        }

        /// <summary>
        /// Gets the list of connected devices.
        /// </summary>
        public static Task<AdbResult> GetDevicesAsync()
        {
            return RunCommandAsync("devices");
        }

        /// <summary>
        /// Gets ADB version information.
        /// </summary>
        public static Task<AdbResult> GetVersionAsync()
        {
            return RunCommandAsync("version");
        }

        /// <summary>
        /// Launches an app by package name.
        /// </summary>
        public static Task<AdbResult> LaunchAppAsync(string deviceSerial, string packageName)
        {
            return RunCommandAsync($"-s {deviceSerial} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1");
        }

        /// <summary>
        /// Stops an app by package name.
        /// </summary>
        public static Task<AdbResult> StopAppAsync(string deviceSerial, string packageName)
        {
            return RunCommandAsync($"-s {deviceSerial} shell am force-stop {packageName}");
        }
    }

    /// <summary>
    /// Result of an ADB command execution.
    /// </summary>
    public class AdbResult
    {
        public bool Success { get; }
        public string Output { get; }

        public AdbResult(bool success, string output)
        {
            Success = success;
            Output = output;
        }
    }
}
