﻿using System.IO;

namespace WallpaperDownloader.Helpers
{
    public static class DirectoryHelper
    {
        public static void EnsurePathDirectoryExists(string path)
        {
            var dirPath = Path.GetDirectoryName(path);
            if (!Directory.Exists(dirPath))
            {
                if (dirPath != null) Directory.CreateDirectory(dirPath);
            }
        }
    }
}
