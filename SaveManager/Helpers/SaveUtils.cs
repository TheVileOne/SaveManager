using System;
using System.IO;
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

        public static bool BackupSaves(string backupPath)
        {
            try
            {
                Directory.CreateDirectory(backupPath);

                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, false);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while copying save backups");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        public static bool RestoreFromBackup(string backupPath)
        {
            try
            {
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, true);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while copying save backups");
                Plugin.Logger.LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// Moves a save file between the persistent data path, and the specified backup path
        /// </summary>
        /// <param name="filename">The file to copy</param>
        /// <param name="backupPath">The path where file backups are stored</param>
        /// <param name="copyingFromBackups">Changes the move destination</param>
        public static void CopySaveFile(string filename, string backupPath, bool copyingFromBackups)
        {
            string sourcePath, destPath;

            if (copyingFromBackups)
            {
                sourcePath = Path.Combine(backupPath, filename);
                destPath = Path.Combine(Application.persistentDataPath, filename);
            }
            else
            {
                sourcePath = Path.Combine(Application.persistentDataPath, filename);
                destPath = Path.Combine(backupPath, filename);
            }

            if (File.Exists(sourcePath))
                FileSystemUtils.SafeCopyFile(sourcePath, destPath);
        }
    }
}
