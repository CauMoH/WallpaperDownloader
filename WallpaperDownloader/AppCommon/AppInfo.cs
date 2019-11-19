using System;
using System.Reflection;

namespace WallpaperDownloader.AppCommon
{
    internal static class AppInfo
    {
        public const string AppName = "WallpaperDownloader";
        public const string AppMutexName = "WallpaperDownloader{7A112583-0FE8-445F-9885-1F345F46E07B}";
        public const string CompanyName = "CauMoH";
        public const string AppRegKey = @"Software\" + CompanyName + @"\" + AppName;

        public const int VkAppId = 7212807;
        public const string ClientSecret = "dPQErQ8yHTGZJA6qLxPf";
        public const int HDWallpaperGroupId = -22786271;


        public static Version Version => Assembly.GetEntryAssembly().GetName().Version;
    }
}
