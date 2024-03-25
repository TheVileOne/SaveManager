using BepInEx;
using BepInEx.Logging;
using MulitversionSupport;
using System;
using System.IO;
using UnityEngine;

namespace MultiversionSupport
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.multiversionsupport"; // This should be the same as the id in modinfo.json!
        public const string PLUGIN_NAME = "Multiversion Support"; // This should be a human-readable version of your mod's name. This is used for log files and also displaying which mods get loaded. In general, it's a good idea to match this with your modinfo.json as well.
        public const string PLUGIN_VERSION = "0.5.0";

        public static new ManualLogSource Logger { get; private set; }

        public static string BackupPath;

        private bool isInitialized;

        public void Awake()
        {
            BackupPath = Path.Combine(Application.persistentDataPath, "backup");

            if (!File.Exists(Application.persistentDataPath))
            {
                Logger.LogWarning("Could not locate permanent data path");
                return;
            }

            Directory.CreateDirectory(BackupPath); //Create in case it doesn't exist
            FileInfoResult result = CollectFileInfo();
        }

        public FileInfoResult CollectFileInfo()
        {
            FileInfoResult result = new FileInfoResult();

            result.CurrentVersion = Application.version;
            result.CurrentVersionPath = Path.Combine(BackupPath, result.CurrentVersion);

            string versionCheckPath = Path.Combine(Application.persistentDataPath, "LastGameVersion.txt");

            //Retrieve the last mod recorded game version from Rain World's persistent data path
            if (File.Exists(versionCheckPath))
            {
                var fileData = File.ReadLines(versionCheckPath).GetEnumerator();

                result.LastVersion = fileData.Current;
                result.LastVersionPath = Path.Combine(BackupPath, result.LastVersion);
            }

            return result;
        }

        public void OnEnable()
        {
            if (isInitialized) return;

            Logger = base.Logger;
            isInitialized = true;
        }
    }
}