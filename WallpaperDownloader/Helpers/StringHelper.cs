using System.IO;

namespace WallpaperDownloader.Helpers
{
    public static class StringHelper
    {
        public static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
