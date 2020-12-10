namespace WallpaperDownloader.EventArgs
{
    public sealed class CaptchaEnteredEventArgs : System.EventArgs
    {
        public CaptchaEnteredEventArgs(ulong captchaSid, string captchaKey)
        {
            CaptchaSid = captchaSid;
            CaptchaKey = captchaKey;
        }

        public ulong CaptchaSid { get; }

        public string CaptchaKey { get; }
    }
}
