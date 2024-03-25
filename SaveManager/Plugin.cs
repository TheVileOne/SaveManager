using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaveManager
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.savemanager"; // This should be the same as the id in modinfo.json!
        public const string PLUGIN_NAME = "Save Manager"; // This should be a human-readable version of your mod's name. This is used for log files and also displaying which mods get loaded. In general, it's a good idea to match this with your modinfo.json as well.
        public const string PLUGIN_VERSION = "1.0.0";

        public static new ManualLogSource Logger { get; private set; }

        public static string BackupPath;

        public void Awake()
        {
            Logger = base.Logger;
            BackupPath = Path.Combine(Application.persistentDataPath, "backup");

            if (!Directory.Exists(Application.persistentDataPath))
            {
                Logger.LogWarning("Could not locate persistent data path");
                return;
            }

            Directory.CreateDirectory(BackupPath); //Create in case it doesn't exist

            FileInfoResult result = CollectFileInfo();

            if (result.CurrentVersion == result.LastVersion)
            {
                //Handle situation where the version hasn't changed since the last time the mod was enabled
                BackupSaves(result.CurrentVersionPath);
                ManageStrayBackups(result.CurrentVersionPath);
            }
            else
            {
                //Handle situations where the version has changed since the last time the mod was enabled
                BackupSaves(result.LastVersionPath); //Current save files should be stored in the last detected version backup folder
                RestoreFromBackup(result.CurrentVersionPath);
                ManageStrayBackups(result.LastVersionPath);
            }

            //Make sure that the version .txt file is matches the current version
            Logger.LogInfo("Creating version file");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "LastGameVersion.txt"), result.CurrentVersion);
        }

        /// <summary>
        /// Retrieves information pertaining to the active, and last active game version
        /// </summary>
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

                fileData.MoveNext();
                result.LastVersion = fileData.Current;
            }

            if (!string.IsNullOrEmpty(result.LastVersion))
                result.LastVersionPath = Path.Combine(BackupPath, result.LastVersion);
            else
            {
                result.LastVersion = result.CurrentVersion;
                result.LastVersionPath = result.CurrentVersionPath;
            }

            Logger.LogInfo("Current Version" + result.CurrentVersion);
            Logger.LogInfo("Last Version" + result.LastVersion);

            return result;
        }

        /// <summary>
        /// Move all backup directories to a given directory path
        /// </summary>
        public void ManageStrayBackups(string backupPath)
        {
            try
            {
                string backupsDir = Path.GetFileName(BackupPath);

                Logger.LogInfo("Looking for stray backup directories");
                List<string> strayBackupDirs = new List<string>();
                foreach (string dir in Directory.GetDirectories(BackupPath, "*", SearchOption.TopDirectoryOnly))
                {
                    string dirName = Path.GetFileName(dir);

                    if (dirName.Length > 25) //No versioning format should contain this many characters
                    {
                        Logger.LogInfo("Found " + dirName);
                        strayBackupDirs.Add(dirName);
                    }
                }

                foreach (string dir in strayBackupDirs)
                    FileSystemUtils.SafeMoveDirectory(BackupPath, Path.Combine(backupPath, dir), SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Logger.LogError("Unable to move backup directories");
                Logger.LogError(ex);
            }
        }

        /// <summary>
        /// Takes files from the persistent data directory and moves them to a backup directory
        /// </summary>
        public bool BackupSaves(string backupPath)
        {
            Logger.LogInfo("Backing up save files");

            if (FileSystemUtils.HasFiles(backupPath))
                MoveDirectoryToAltPath(backupPath);

            return SaveUtils.BackupSaves(backupPath);
        }

        /// <summary>
        /// Takes files from a backup directory and moves them into the persistent data directory
        /// </summary>
        public bool RestoreFromBackup(string backupPath)
        {
            Logger.LogInfo("Restoring save files");

            //Check that there are version specific backup files for the current version of the game
            if (FileSystemUtils.HasFiles(backupPath))
                return SaveUtils.RestoreFromBackup(backupPath);

            return false;
        }

        /// <summary>
        /// Copies a directory and its contents to a secondary folder
        /// </summary>
        public void MoveDirectoryToAltPath(string sourcePath)
        {
            string altPath = Path.Combine(sourcePath, "old");

            //Delete any preexisting folders in the reserve path to make room for new backup storage 
            if (Directory.Exists(altPath))
                FileSystemUtils.SafeDeleteDirectory(altPath);

            //Copy any existing files to a reserve directory
            FileSystemUtils.CopyDirectory(sourcePath, altPath, SearchOption.AllDirectories);
        }
    }
}