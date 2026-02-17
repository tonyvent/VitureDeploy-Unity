using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Viture.Deployer.Editor
{
    /// <summary>
    /// Persistent settings for the Viture Deployer plugin.
    /// Stored in the user's local application data folder.
    /// </summary>
    [Serializable]
    public class VitureDeployerSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VitureDeployer",
            "settings.json");

        public string PairIp = "192.168.";
        public string PairPort = "";
        public string ConnectIp = "192.168.";
        public string ConnectPort = "5555";
        public string LastApkPath = "";
        public bool ShowAllApps = false;
        public bool AutoDeployAfterBuild = false;
        public List<SavedDevice> SavedDevices = new List<SavedDevice>();

        /// <summary>
        /// Loads settings from disk, or returns defaults if not found.
        /// </summary>
        public static VitureDeployerSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonUtility.FromJson<VitureDeployerSettings>(json) ?? new VitureDeployerSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VitureDeployer] Failed to load settings: {ex.Message}");
            }

            return new VitureDeployerSettings();
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VitureDeployer] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds or updates a saved device.
        /// </summary>
        public void SaveDevice(string name, string ip, string port)
        {
            var existing = SavedDevices.Find(d => d.Ip == ip);
            if (existing != null)
            {
                existing.Port = port;
                existing.LastConnected = DateTime.Now.ToString("o");
            }
            else
            {
                SavedDevices.Add(new SavedDevice
                {
                    Name = name,
                    Ip = ip,
                    Port = port,
                    LastConnected = DateTime.Now.ToString("o")
                });
            }
            Save();
        }

        /// <summary>
        /// Removes a saved device by IP.
        /// </summary>
        public void RemoveDevice(string ip)
        {
            SavedDevices.RemoveAll(d => d.Ip == ip);
            Save();
        }
    }

    /// <summary>
    /// Represents a saved device for quick connection.
    /// </summary>
    [Serializable]
    public class SavedDevice
    {
        public string Name = "";
        public string Ip = "";
        public string Port = "5555";
        public string LastConnected = "";

        // For display and sorting
        public DateTime LastConnectedDate
        {
            get
            {
                if (DateTime.TryParse(LastConnected, out var date))
                    return date;
                return DateTime.MinValue;
            }
        }
    }
}
