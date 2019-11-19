﻿using System.Collections.Generic;
using System.Linq;
using WallpaperDownloader.Configuration;

namespace WallpaperDownloader.EventArgs
{
    public sealed class SettingsSavedEventArgs : System.EventArgs
    {
        public SettingsSavedEventArgs(IEnumerable<ChangeProp> changedPropNames)
        {
            ChangedPropNames = changedPropNames;
        }

        public IEnumerable<ChangeProp> ChangedPropNames { get; }

        public bool IsChanged(string name)
        {
            return ChangedPropNames.Any(c => c.PropName.ToString() == name);
        }
    }
}
