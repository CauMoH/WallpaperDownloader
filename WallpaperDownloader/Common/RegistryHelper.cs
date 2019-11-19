using System;
using Microsoft.Win32;

namespace WallpaperDownloader.Common
{
    public static class RegistryHelper
    {
        public static readonly RegistryHive Hklm = new RegistryHive(Registry.LocalMachine);
        public static readonly RegistryHive Hkcu = new RegistryHive(Registry.CurrentUser);

        public const string AutoRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static readonly string DefaultValueName = string.Empty;

        public sealed class RegistryHive
        {
            private readonly RegistryKey _rootKey;

            public RegistryHive(RegistryKey rootKey)
            {
                _rootKey = rootKey;
            }

            public TValue Get<TValue>(string keyName, string valueName, TValue defaultValue = default(TValue))
            {
                return GetValue(_rootKey, keyName, valueName, val => (TValue)val, defaultValue);
            }

            public TValue Get<TValue>(string keyName, string valueName, Func<object, TValue> convertFunc, TValue defaultValue = default(TValue))
            {
                return GetValue(_rootKey, keyName, valueName, convertFunc, defaultValue);
            }

            private static TValue GetValue<TValue>(RegistryKey parent, string keyName, string valueName, Func<object, TValue> convertFunc,
                TValue defaultValue = default(TValue))
            {
                using (var key = parent.OpenSubKey(keyName))
                {
                    if (key == null)
                    {
                        return defaultValue;
                    }

                    var value = key.GetValue(valueName);
                    if (value == null)
                    {
                        return defaultValue;
                    }

                    return convertFunc(value);
                }
            }

            public void Set(string keyName, string valueName, object value, RegistryValueKind valueKind = RegistryValueKind.Unknown)
            {
                using (var key = _rootKey.CreateSubKey(keyName))
                {
                    key?.SetValue(valueName, value, valueKind);
                }
            }

            public bool IsExist(string keyName, string valueName)
            {
                using (var key = _rootKey.OpenSubKey(keyName))
                {
                    return key?.GetValue(valueName) != null;
                }
            }

            public void Delete(string keyName, string valueName)
            {
                using (var key = _rootKey.OpenSubKey(keyName, writable: true))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.DeleteValue(valueName, throwOnMissingValue: false);
                }
            }
        }
    }
}
