using System;
using System.IO;

namespace WallpaperDownloader.AppCommon
{
    internal static class PathHelper
    {
        private const string ConfigFileName = "config.xml";

        private const string PhotoFolderName = "Download";

        private const string PhotoFileName = "tmp.jpg";

        public static string AppDataFolderPath
        {
            get
            {
                var rootFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(rootFolder, AppInfo.AppName);
            }
        }

        public static string PhotoFolderPath => Path.Combine(AppDataFolderPath, PhotoFolderName);

        public static string ConfigFilePath => Path.Combine(AppDataFolderPath, ConfigFileName);

        public static string PhotoFilePath => Path.Combine(PhotoFolderPath, PhotoFileName);
    }
}
