using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SaveManager.Helpers
{
    public static class SaveUtils
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

        public static bool BackupSaves(string backupPath)
        {
            try
            {
                Directory.CreateDirectory(Plugin.BackupOverwritePath);

                short errorCodeHandle = -1;
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, false, ref errorCodeHandle);

                if (errorCodeHandle != -1)
                    handleError(errorCodeHandle);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while copying save file");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        public static bool RestoreFromBackup(string backupPath)
        {
            try
            {
                Directory.CreateDirectory(Plugin.BackupOverwritePath);

                short errorCodeHandle = -1;
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, true, ref errorCodeHandle);

                if (errorCodeHandle != -1)
                    handleError(errorCodeHandle);
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
        /// Moves a save file between the persistent data path, and a specified directory
        /// </summary>
        /// <param name="filename">The file to copy</param>
        /// <param name="targetPath">The path where a file is copied to, or copied from</param>
        /// <param name="copyingFromTargetPath">Identifies the source location of the file</param>
        public static void CopySaveFile(string filename, string targetPath, bool copyingFromTargetPath, ref short copyErrorCode)
        {
            string sourcePath, destPath;

            if (copyingFromTargetPath)
            {
                sourcePath = Path.Combine(targetPath, filename);
                destPath = Path.Combine(Application.persistentDataPath, filename);
            }
            else
            {
                sourcePath = Path.Combine(Application.persistentDataPath, filename);
                destPath = Path.Combine(targetPath, filename);
            }

            //Make sure that any existing files get transferred to the last-overwrite directory before getting copied over
            if (File.Exists(destPath))
            {
                if (!FileSystemUtils.SafeMoveFile(destPath, Path.Combine(Plugin.BackupOverwritePath, filename)))
                    handleError(1, ref copyErrorCode);
            }

            //Copy file to destination
            if (File.Exists(sourcePath))
            {
                if (!FileSystemUtils.SafeCopyFile(sourcePath, destPath))
                    handleError(0, ref copyErrorCode);
            }

            void handleError(short errorCode, ref short errorCodeHandle)
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

        public static string GetRecentBackupPath()
        {
            //Check that we should look for the typical timestamped backup folders instead of the overwrite directory
            if (BackupsCreatedThisSession || !ContainsSaveFiles(Plugin.BackupOverwritePath))
            {
                string mostRecentBackupDirectory = GetRecentBackupPath(Path.Combine(Plugin.BackupPath, Plugin.GameVersionString));

                //Handle situation where there are no directories found
                if (mostRecentBackupDirectory == null)
                {
                    //This is very unlikely to trigger. The contents of the backup folder probably was changed by the user
                    if (BackupsCreatedThisSession && ContainsSaveFiles(Plugin.BackupOverwritePath))
                        return Plugin.BackupOverwritePath;
                }
                return mostRecentBackupDirectory;
            }
            return Plugin.BackupOverwritePath;
        }

        public static string GetRecentBackupPath(string path)
        {
            //Get backups from the version specific directory
            string[] backupDirs = Directory.GetDirectories(Path.Combine(Plugin.BackupPath, Plugin.GameVersionString));

            string mostRecentBackupDirectory = null;
            long mostRecentCreationDateInSeconds = 0;
            foreach (string dir in backupDirs)
            {
                string directoryName = Path.GetDirectoryName(dir);

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
