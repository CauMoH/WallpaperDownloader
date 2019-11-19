using System;
using WallpaperDownloader.Enums;

namespace WallpaperDownloader.Data
{
    public class Period
    {
        public WallpaperUpdateType Type { get; set; }
        public string Description { get; set; }
        public TimeSpan TimeSpanPeriod { get; set; }

        public Period(WallpaperUpdateType type, string description, TimeSpan timeSpanPeriod)
        {
            Type = type;
            Description = description;
            TimeSpanPeriod = timeSpanPeriod;
        }
    }
}
