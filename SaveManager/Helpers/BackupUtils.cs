﻿using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SaveManager.Helpers
{
    public static class BackupUtils
    {
        public static string[] SaveFiles =
        {
            "sav",
            "sav2",
            "sav3",
            "expCore",
            "expCore1",
            "expCore2",
            "expCore3",
            "exp1",
            "exp2",
            "exp3"
        };

        /// <summary>
        /// A flag that indicates that the user has created at least one backup through the Remix option interface
        /// </summary>
        public static bool BackupsCreatedThisSession;

        /// <summary>
        /// A flag that indicates that a Restore From Backup request has been handled before any user-created backups have been created
        /// since Rain World has been running
        /// </summary>
        public static bool RestoreHandledWithoutBackup;

        /// <summary>
        /// Checks directory path for save files
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="checkSpecificFiles">Whether each save file should be checked individually, or to assume all files are save files</param>
        public static bool ContainsSaveFiles(string path, bool checkSpecificFiles = false)
        {
            if (!checkSpecificFiles)
                return FileSystemUtils.HasFiles(path);

            DirectoryInfo dir = new DirectoryInfo(path);

            //Check that directory contains at least one save file
            if (dir.Exists)
                return Array.Exists(dir.GetFiles(), f => SaveFiles.Contains(f.Name));
            return false;
        }

        /// <summary>
        /// Creates a backup using a standard backup name format 
        /// </summary>
        public static bool BackupSaves()
        {
            string backupName = GenerateBackupName();
            string backupTargetPath = Path.Combine(GetBackupTargetPath(false), backupName);

            return BackupSaves(backupTargetPath);
        }

        public static bool BackupSaves(string backupPath)
        {
            try
            {
                if (!ContainsSaveFiles(Application.persistentDataPath, true))
                {
                    Plugin.Logger.LogWarning("No save files to backup");
                    return false;
                }

                Directory.CreateDirectory(backupPath); //Exception will trigger if directory doesn't exist

                bool targetingOverwritePath = backupPath == Plugin.BackupOverwritePath;
                string overwritePath = createOverwriteDirectory(targetingOverwritePath);

                short errorCodeHandle = -1;
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, false, overwritePath, ref errorCodeHandle);

                if (errorCodeHandle != -1)
                    handleError(errorCodeHandle);

                //Backup process shouldn't be targeting this directory directly. It should be okay to discard temp storage
                if (targetingOverwritePath)
                {
                    if (PathUtils.GetDirectoryName(overwritePath) != "temp")
                        throw new InvalidOperationException("This operation only accepts the temp directory");

                    FileSystemUtils.SafeDeleteDirectory(overwritePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while copying save file");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a backup of active save files removing them from the persistent data folder
        /// </summary>
        public static void RemoveSaves()
        {
            if (!BackupSaves())
            {
                Plugin.Logger.LogWarning("Save backup failed - Aborting process");
                return;
            }

            RemoveSavesNoBackup(Path.Combine(Application.persistentDataPath));
        }

        public static void RemoveSavesNoBackup(string path)
        {
            foreach (string file in SaveFiles)
                FileSystemUtils.SafeDeleteFile(PathUtils.Combine(path, file));
        }

        public static bool RestoreFromBackup(string backupPath)
        {
            try
            {
                //There will be issues when transferring files directly to, or from this directory
                bool targetingOverwritePath = backupPath == Plugin.BackupOverwritePath;
                string overwritePath = createOverwriteDirectory(targetingOverwritePath);

                short errorCodeHandle = -1;
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, true, overwritePath, ref errorCodeHandle);

                if (errorCodeHandle != -1)
                    handleError(errorCodeHandle);

                if (targetingOverwritePath)
                {
                    if (PathUtils.GetDirectoryName(overwritePath) != "temp")
                        throw new InvalidOperationException("This operation only accepts the temp directory");

                    RemoveSavesNoBackup(Plugin.BackupOverwritePath); //Remove saves to avoid mixing backup files

                    //Move all files from the temp directory back into the actual overwrite directory
                    FileSystemUtils.SafeMoveDirectory(overwritePath, Plugin.BackupOverwritePath, SearchOption.TopDirectoryOnly);
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while copying save files");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// Ensures that a valid overwrite directory is used during the copy process
        /// </summary>
        private static string createOverwriteDirectory(bool useTempPath)
        {
            Directory.CreateDirectory(Plugin.BackupOverwritePath);

            if (useTempPath)
            {
                Plugin.Logger.LogInfo("Creating temp directory");

                string overwritePath = PathUtils.Combine(Plugin.BackupPath, "temp");

                Directory.CreateDirectory(overwritePath);
                return overwritePath;
            }
            return Plugin.BackupOverwritePath;
        }

        /// <summary>
        /// Moves a save file between the persistent data path, and a specified directory
        /// </summary>
        /// <param name="filename">The file to copy</param>
        /// <param name="targetPath">The path where a file is copied to, or copied from</param>
        /// <param name="copyingFromTargetPath">Identifies the source location of the file</param>
        public static void CopySaveFile(string filename, string targetPath, bool copyingFromTargetPath, string overwritePath, ref short copyErrorCode)
        {
            string sourcePath, destPath;

            if (copyingFromTargetPath)
            {
                sourcePath = PathUtils.Combine(targetPath, filename);
                destPath = PathUtils.Combine(Application.persistentDataPath, filename);
            }
            else
            {
                sourcePath = PathUtils.Combine(Application.persistentDataPath, filename);
                destPath = PathUtils.Combine(targetPath, filename);
            }

            //Make sure that any existing files get transferred to the last-overwrite directory before getting copied over
            if (File.Exists(destPath))
            {
                if (!FileSystemUtils.SafeMoveFile(destPath, PathUtils.Combine(overwritePath, filename)))
                    handleError(1, ref copyErrorCode);
            }

            //Copy file to destination
            if (File.Exists(sourcePath))
            {
                if (!FileSystemUtils.SafeCopyFile(sourcePath, destPath))
                    handleError(0, ref copyErrorCode);
            }

            static void handleError(short errorCode, ref short errorCodeHandle)
            {
                if (errorCodeHandle == -1 || errorCodeHandle == errorCode)
                {
                    errorCodeHandle = errorCode;
                    return;
                }

                /*
                 * Code 0: Copy to target failure
                 * Code 1: Move to overwrite folder failure
                 * Code 2: Mixed failures
                 */
                if ((errorCode == 0 && errorCodeHandle == 1) || (errorCode == 1 && errorCodeHandle == 0))
                    errorCodeHandle = 2;
            }
        }

        /// <summary>
        /// Gets the path that backup directories should be created and stored
        /// </summary>
        public static string GetBackupTargetPath(bool relativePathOnly)
        {
            string backupTargetPath = relativePathOnly ? Plugin.BACKUP_MAIN_FOLDER_NAME : Plugin.BackupPath;

            //Use the version directory when it exists, and per version saving is enabled 
            if (Config.PerVersionSaving)
            {
                string perVersionBackupTargetPath = PathUtils.Combine(backupTargetPath, Plugin.GameVersionString);

                //When the logic is enabled on startup, directory is guaranteed to exist, not the case if set through Remix menu.
                if (Plugin.VersionSavingEnabledOnStartUp || Directory.Exists(perVersionBackupTargetPath))
                    backupTargetPath = perVersionBackupTargetPath;
            }
            return backupTargetPath;
        }

        public static string GetRecentBackupPath()
        {
            //Check that we should look for the typical timestamped backup folders instead of the overwrite directory
            if (BackupsCreatedThisSession || !ContainsSaveFiles(Plugin.BackupOverwritePath))
            {
                //Both of these paths may have valid backup directories such as if per version saving was toggled in the Remix menu
                string mostRecentBackupDirectory = GetRecentBackupPath(PathUtils.Combine(Plugin.BackupPath, Plugin.GameVersionString));
                string mostRecentBackupDirectoryFromBase = GetRecentBackupPath(Plugin.BackupPath);

                if (mostRecentBackupDirectory != null)
                {
                    if (mostRecentBackupDirectoryFromBase != null)
                    {
                        //See GetRecentBackupPath(string) for explanation of this code
                        long creationDateInSeconds, creationDateInSecondsFromBase;

                        string directoryName = PathUtils.GetDirectoryName(mostRecentBackupDirectory);
                        string directoryNameFromBase = PathUtils.GetDirectoryName(mostRecentBackupDirectoryFromBase);

                        int sepIndex = directoryName.IndexOf('_'); //We need the time in seconds
                        creationDateInSeconds = long.Parse(directoryName.Substring(0, sepIndex));

                        sepIndex = directoryNameFromBase.IndexOf('_');
                        creationDateInSecondsFromBase = long.Parse(directoryNameFromBase.Substring(0, sepIndex));

                        //We found a more recent backup in the base Backup directory
                        if (creationDateInSecondsFromBase > creationDateInSeconds)
                            mostRecentBackupDirectory = mostRecentBackupDirectoryFromBase;
                    }
                }
                else if (mostRecentBackupDirectoryFromBase != null)
                {
                    mostRecentBackupDirectory = mostRecentBackupDirectoryFromBase;
                }
                else if (BackupsCreatedThisSession && ContainsSaveFiles(Plugin.BackupOverwritePath))
                {
                    //This is very unlikely to trigger. The contents of the backup folder probably was changed by the user
                    mostRecentBackupDirectory = Plugin.BackupOverwritePath;
                }
                return mostRecentBackupDirectory;
            }
            return Plugin.BackupOverwritePath;
        }

        public static string GetRecentBackupPath(string path)
        {
            if (!Directory.Exists(path)) return null;

            //Get backups from the version specific directory
            string[] backupDirs = Directory.GetDirectories(path);

            string mostRecentBackupDirectory = null;
            long mostRecentCreationDateInSeconds = 0;
            foreach (string dir in backupDirs)
            {
                string directoryName = PathUtils.GetDirectoryName(dir);

                if (directoryName != Plugin.BACKUP_OVERWRITE_FOLDER_NAME)
                {
                    /*
                     * File format (separated by underscores)
                     * 0 - Total time since file creation and some hardcoded year in seconds
                     * 1 - Date format "yyyy-MM-dd"
                     * 2 - Date format (hours/minutes) "HH-mm"
                     * 3 - Only new format - contains USR to indicate user created backup
                     */

                    int sepIndex = directoryName.IndexOf('_'); //We need the time in seconds

                    if (sepIndex == -1)
                        continue;

                    string timeString = directoryName.Substring(0, sepIndex);

                    long creationDateInSeconds = -1;
                    if (!long.TryParse(timeString, out creationDateInSeconds))
                        continue;

                    if (creationDateInSeconds > mostRecentCreationDateInSeconds && ContainsSaveFiles(dir))
                    {
                        mostRecentCreationDateInSeconds = creationDateInSeconds;
                        mostRecentBackupDirectory = dir;
                    }
                }
            }
            return mostRecentBackupDirectory;
        }

        /// <summary>
        /// Renames directory specified by targetPath to match timestamp format, and attempts to move it to backup directory
        /// </summary>
        /// <param name="targetPath">The directory path to convert</param>
        public static void ConvertToBackupFormat(string targetPath)
        {
            string backupName = GenerateBackupName();
            string backupTargetPath = GetBackupTargetPath(false);

            FileSystemUtils.SafeMoveDirectory(targetPath, PathUtils.Combine(backupTargetPath, backupName), SearchOption.AllDirectories);
        }

        /// <summary>
        /// Creates a unique backup directory name using DateTime formatting
        /// </summary>
        public static string GenerateBackupName()
        {
            long totalSeconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            return totalSeconds + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        }

        private static void handleError(int errorCode)
        {
            string errorMessage = "Unknown file error occurred";
            if (errorCode == 0)
                errorMessage = "One or more save files failed to copy";
            else if (errorCode == 1)
                errorMessage = "One or more save files failed to move to the overwrite backup directory";
            else if (errorCode == 2)
                errorMessage = "Multiple issues occurred while copying save files";

            Plugin.Logger.LogWarning(errorMessage);
        }
    }
}
