using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaveManager.Helpers
{
    public static class PathUtils
    {
        public static readonly char[] DirectorySeparators = new char[] { '\\', '/' };

        /// <summary>
        /// Retrieves the directory name, without the filename, and other path information
        /// </summary>
        public static string GetDirectoryName(string path)
        {
            if (Path.HasExtension(path))
                path = Path.GetDirectoryName(path); //Gets the full path of the containing directory

            if (string.IsNullOrEmpty(path))
                return path;

            //Trim for consistency
            path = path.Trim().TrimEnd(DirectorySeparators);

            //Find the last directory separator
            int sepIndex = path.LastIndexOfAny(DirectorySeparators);

            //If one exists, return the data to the right of it, or the entire path string otherwise
            return sepIndex != -1 ? path.Substring(sepIndex + 1) : path;
        }

        public static string Combine(params string[] pathSegments)
        {
            return NormalizePath(Path.Combine(pathSegments));
        }

        /// <summary>
        /// Formats a path to a consistent format
        /// </summary>
        public static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}
