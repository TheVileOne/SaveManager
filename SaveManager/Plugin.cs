using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using SaveManager.Helpers;
using SaveManager.Interface;
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

        /// <summary>
        /// The path to the backup folder
        /// </summary>
        public static string BackupPath;

        private static string _overwritePath;
        /// <summary>
        /// The path to the backup folder used to store save files that need to be overwritten by the mod
        /// </summary>
        public static string BackupOverwritePath
        {
            set => _overwritePath = value;
            get
            {
                if (_overwritePath == null)
                    return Path.Combine(BackupPath, GameVersionString, ConfigFilePath, BACKUP_OVERWRITE_FOLDER_NAME);
                return _overwritePath;
            }
        }

        public const string BACKUP_OVERWRITE_FOLDER_NAME = "last-overwrite";

        /// <summary>
        /// The path to a check file that is used to check if the mod backs up save files successfully
        /// </summary>
        public static string BackupSuccessCheckPath;
        public static string ConfigFilePath;
        public static string GameVersionString;

        public static CustomOptionInterface OptionInterface;

        /// <summary>
        /// A flag that indicates that version saving was always enabled as compared to enabled via the Remix menu
        /// </summary>
        private static bool versionSavingEnabledOnStartUp;

        public void Awake()
        {
            Logger = base.Logger;
            BackupPath = Path.Combine(Application.persistentDataPath, "backup");
            ConfigFilePath = Path.Combine(Application.persistentDataPath, "ModConfigs", PLUGIN_GUID + ".txt");

            SaveManager.Config.Load();

            On.RainWorld.Start += RainWorld_Start;
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
        {
            //Use reflection to prevent const from being hardcoded into mod on build
            GameVersionString = ((string)typeof(RainWorld).GetField(nameof(RainWorld.GAME_VERSION_STRING)).GetValue(null)).TrimStart('v');

            if (!Directory.Exists(Application.persistentDataPath))
            {
                Logger.LogWarning("Could not locate persistent data path");
                orig(self);
                return;
            }

            //BackupFrequency backupFrequency = (BackupFrequency)SaveManager.Config.GetValue(nameof(SaveManager.Config.cfgBackupFrequency), 0);

            //Logger.LogInfo(backupFrequency);

            try
            {
                Directory.CreateDirectory(BackupPath); //Create in case it doesn't exist

                string lastVersionFilePath = Path.Combine(Application.persistentDataPath, "LastGameVersion.txt");

                if (SaveManager.Config.PerVersionSaving)
                {
                    versionSavingEnabledOnStartUp = true;

                    FileInfoResult result = CollectFileInfo();

                    BackupSuccessCheckPath = Path.Combine(Application.persistentDataPath, "savemanager-check.txt");

                    if (!File.Exists(BackupSuccessCheckPath)) //Create a file to let us know if save data is backed up when Rain World shuts down
                    {
                        //This directory stores save files that will be replaced by the mod 
                        BackupOverwritePath = Path.Combine(result.CurrentVersionPath, BACKUP_OVERWRITE_FOLDER_NAME);
                        File.Create(BackupSuccessCheckPath);

                        //Check for save files specific to the current game version
                        if (!Helpers.SaveUtils.ContainsSaveFiles(result.CurrentVersionPath))
                        {
                            //Creates copy of current save files in version-specific folder
                            BackupSaves(result.CurrentVersionPath);
                        }
                        else
                        {
                            //Replaces current save files with save files from version-specific folder
                            RestoreFromBackup(result.CurrentVersionPath);
                        }

                        //When there is a version mismatch, presume that strays are from the past version, and not the current one
                        ManageStrayBackups(result.CurrentVersion != result.LastVersion ?
                            result.LastVersionPath : result.CurrentVersionPath);
                    }
                    else
                    {
                        Logger.LogWarning("Save data backup was unsuccessful on game exit. Backing up current saves.");

                        //Hopefully this never triggers, but if it does, the save files on old patch builds cannot be salvaged due to
                        //a save bug caused by format compatibilities with newer versions. This logic is fine if the version is unchanged.
                        if (result.CurrentVersion == result.LastVersion || !result.CurrentVersion.StartsWith("1.9.1"))
                        {
                            BackupOverwritePath = Path.Combine(result.LastVersionPath, BACKUP_OVERWRITE_FOLDER_NAME);
                            BackupSaves(result.LastVersionPath);
                        }
                    }

                    /*
                    //Old logic
                    if (result.CurrentVersion == result.LastVersion)
                    {
                        //Handle situation where the version hasn't changed since the last time the mod was enabled
                        if (backupFrequency == BackupFrequency.Always || !File.Exists(result.LastVersionPath))
                            BackupSaves(result.CurrentVersionPath);
                        ManageStrayBackups(result.CurrentVersionPath);
                    }
                    else
                    {
                        //Handle situations where the version has changed since the last time the mod was enabled
                        if (backupFrequency != BackupFrequency.Never)
                        {
                            //Only newer versions support LastVersion backups. Older versions overwrite saves before any mod can activate
                            bool allowBackup = result.CurrentVersion.StartsWith("1.9.1") || result.CurrentVersion.StartsWith("2");

                            if (allowBackup)
                                BackupSaves(result.LastVersionPath); //Current save files should be stored in the last detected version backup folder
                            RestoreFromBackup(result.CurrentVersionPath);
                        }
                        ManageStrayBackups(result.LastVersionPath);
                    }
                    */

                    if (result.CurrentVersion != result.LastVersion || !File.Exists(lastVersionFilePath))
                        createVersionFile(result.CurrentVersion);
                }
                else
                {
                    //To be safe, this file shouldn't be allowed to contain a stale version while per version saves is disabled
                    FileSystemUtils.SafeDeleteFile(lastVersionFilePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }

            orig(self);
        }

        private void createVersionFile(string versionText)
        {
            Logger.LogInfo("Creating version file");

            string filePath = "LastGameVersion.txt";
            Exception fileError = null;
            bool writeSuccess = false;
            int writeAttempts = 2;
            string writePath;
            while (writeAttempts != 0)
            {
                //Make sure that the version .txt file matches the current version
                try
                {
                    writePath = Path.Combine(Application.persistentDataPath, filePath);

                    File.WriteAllText(writePath, versionText);
                    writeSuccess = true;
                    writeAttempts = 0;
                }
                catch (Exception ex)
                {
                    if (fileError == null)
                        fileError = ex;
                    writeAttempts--;
                }
            }

            if (fileError != null)
            {
                if (!writeSuccess)
                {
                    Logger.LogError("Failed to overwrite LastGameVersion.txt");
                    Logger.LogError(fileError);
                }
                else
                {
                    Logger.LogWarning("LastGameVersion.txt overwritted with errors");
                }
            }
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            On.RainWorld.OnDestroy += RainWorld_OnDestroy;
            orig(self);

            try
            {
                IL.PlayerProgression.CreateCopyOfSaves += PlayerProgression_CreateCopyOfSaves;
                On.PlayerProgression.CopySaveFile += PlayerProgression_CopySaveFile;

                if (OptionInterface == null)
                {
                    OptionInterface = new CustomOptionInterface();

                    SaveManager.Config.Initialize();
                }

                MachineConnector.SetRegisteredOI(PLUGIN_GUID, OptionInterface);
            }
            catch (Exception ex)
            {
                Logger.LogError("Config did not initialize properly");
                Logger.LogError(ex);
            }
        }

        private void RainWorld_OnDestroy(On.RainWorld.orig_OnDestroy orig, RainWorld self)
        {
            if (BackupSuccessCheckPath != null || (SaveManager.Config.PerVersionSaving && !versionSavingEnabledOnStartUp)) //Mod logic hasn't been applied if this is null
            {
                //Apply any progress made to existing saves, or new saves while the game has been running to the version-specific backup
                BackupSaves(Path.Combine(BackupPath, GameVersionString));
                FileSystemUtils.SafeDeleteFile(BackupSuccessCheckPath); //Remove file created by the mod when Rain World starts
            }
            orig(self);
        }

        private void PlayerProgression_CreateCopyOfSaves(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.After, x => x.MatchLdstr("backup")))
                cursor.EmitDelegate(getRelativeBackupPath); //Change path to version-specific directory
            else
                Logger.LogError("Unable to apply Save Manager IL hook");
        }

        private string getRelativeBackupPath(string backupDir)
        {
            return SaveManager.Config.PerVersionSaving ? Path.Combine(backupDir, GameVersionString) : backupDir;
        }

        /// <summary>
        /// Hook avoids a exception when the save content already exists
        /// </summary>
        private void PlayerProgression_CopySaveFile(On.PlayerProgression.orig_CopySaveFile orig, PlayerProgression self, string sourceName, string destinationDirectory)
        {
            string currentPath = Path.Combine(Application.persistentDataPath, sourceName);
            string destPath = Path.Combine(destinationDirectory, sourceName);
            if (File.Exists(destPath) && File.Exists(currentPath)) //Original code doesn't allow file overwrites and breaks
                FileSystemUtils.SafeCopyFile(destPath, currentPath);
            else
                orig(self, sourceName, destinationDirectory);

            //Handle files being ignored by the backup
            if (sourceName == "exp1")
            {
                self.CopySaveFile("exp2", destinationDirectory);
                self.CopySaveFile("exp3", destinationDirectory);
            }
        }

        /// <summary>
        /// Retrieves information pertaining to the active, and last active game version
        /// </summary>
        public FileInfoResult CollectFileInfo()
        {
            FileInfoResult result = new FileInfoResult();

            string saveDataPath = BackupPath;

            result.CurrentVersion = GameVersionString;
            result.CurrentVersionPath = Path.Combine(saveDataPath, result.CurrentVersion);

            string versionCheckPath = Path.Combine(Application.persistentDataPath, "LastGameVersion.txt");

            //Retrieve the last mod recorded game version from Rain World's persistent data path
            if (File.Exists(versionCheckPath))
            {
                var fileData = File.ReadLines(versionCheckPath).GetEnumerator();

                fileData.MoveNext();
                result.LastVersion = fileData.Current;
                fileData.Dispose();
            }

            if (!string.IsNullOrEmpty(result.LastVersion))
                result.LastVersionPath = Path.Combine(saveDataPath, result.LastVersion);
            else
            {
                result.LastVersion = result.CurrentVersion;
                result.LastVersionPath = result.CurrentVersionPath;
            }

            Logger.LogInfo("Current Version " + result.CurrentVersion);
            Logger.LogInfo("Last Version " + result.LastVersion);

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

                Logger.LogInfo("Checking for stray backup directories");
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
                {
                    //Move the current backup directory within a version-specific backup directory
                    string currentPath = Path.Combine(BackupPath, dir);
                    string destPath = Path.Combine(backupPath, dir);

                    FileSystemUtils.SafeMoveDirectory(currentPath, destPath, SearchOption.AllDirectories);
                }
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
            return Helpers.SaveUtils.BackupSaves(backupPath);
        }

        /// <summary>
        /// Takes files from a backup directory and moves them into the persistent data directory
        /// </summary>
        public bool RestoreFromBackup(string backupPath)
        {
            Logger.LogInfo("Checking for save data for version " + GameVersionString);

            //Check that there are version specific backup files for the current version of the game
            if (FileSystemUtils.HasFiles(backupPath))
            {
                Logger.LogInfo("Restoring save files");
                return Helpers.SaveUtils.RestoreFromBackup(backupPath);
            }

            Logger.LogInfo("No save data available to restore");
            return false;
        }
    }

    public enum BackupFrequency
    {
        VersionChanged,
        Always,
        Never,
    }
}