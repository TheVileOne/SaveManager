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
                Directory.CreateDirectory(backupPath);

                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, false);

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
                foreach (string file in SaveFiles) //Copy each save file into the specified path
                    CopySaveFile(file, backupPath, true);

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
        public static void CopySaveFile(string filename, string targetPath, bool copyingFromTargetPath)
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

            if (File.Exists(sourcePath))
                FileSystemUtils.SafeCopyFile(sourcePath, destPath);
        }
    }
}
