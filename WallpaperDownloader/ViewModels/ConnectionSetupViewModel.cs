using System;
using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using WallpaperDownloader.AppCommon;
using WallpaperDownloader.Common;
using WallpaperDownloader.Configuration;
using WallpaperDownloader.Extension;
using WallpaperDownloader.Views;

namespace WallpaperDownloader.ViewModels
{
    public class ConnectionSetupViewModel : BindableBase
    {
        #region Members

        private ConnectionSetupView _setupView;
        private readonly UserSettings _userSettings;
        private string _username;
        private SecureString _password;
        private bool _isOpen;
        private bool _autoRun;
        private const string IsFirstRunValue = "IsFirstRun";

        #endregion

        #region Props

        public PasswordBoxExtension PbExt { get; internal set; }

        public string UserName
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public SecureString Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// Флаг открытых настроек
        /// </summary>
        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        public bool AutoRun
        {
            get => _autoRun;
            set
            {
                if (value != _autoRun)
                {
                    _autoRun = value;
                    RaisePropertyChanged(nameof(AutoRun));

                    SaveRegistryValues();
                }

            }
        }

        public bool IsFirstRun { get; private set; }

        #endregion

        public ConnectionSetupViewModel(UserSettings userSettings)
        {
            _userSettings = userSettings;
            InitCommands();
        }

        /// <summary>
        /// Открыть настройки
        /// </summary>
        public void OpenConnectionSettings()
        {
            InitSettings();
            IsOpen = true;

            _setupView = new ConnectionSetupView
            {
                DataContext = this,
                Owner = Application.Current.MainWindow
            };

            _setupView.Show();
        }

        /// <summary>
        /// Проинициализировать настройки
        /// </summary>
        private void InitSettings()
        {
            UserName = _userSettings.UserName;
            Password = _userSettings.Password;
            
            if (!RegistryHelper.Hkcu.IsExist(AppInfo.AppRegKey, IsFirstRunValue))
            {
                RegistryHelper.Hkcu.Set(AppInfo.AppRegKey, IsFirstRunValue, Convert.ToInt32(true));
            }

            IsFirstRun = RegistryHelper.Hkcu.Get(AppInfo.AppRegKey, IsFirstRunValue, Convert.ToBoolean, defaultValue: true);

            if (IsFirstRun)
            {
                RegistryHelper.Hkcu.Set(AppInfo.AppRegKey, IsFirstRunValue, Convert.ToInt32(false));
                AutoRun = true;
            }
            else
            {
                AutoRun = RegistryHelper.Hkcu.IsExist(RegistryHelper.AutoRunKey, AppInfo.AppName);
            }
        }

        private void SaveRegistryValues()
        {
            if (AutoRun)
            {
                RegistryHelper.Hkcu.Set(RegistryHelper.AutoRunKey, AppInfo.AppName, Assembly.GetEntryAssembly().Location);
            }
            else
            {
                RegistryHelper.Hkcu.Delete(RegistryHelper.AutoRunKey, AppInfo.AppName);
            }
        }

        #region Commands

        /// <summary>
        /// Инициализация команд
        /// </summary>
        private void InitCommands()
        {
            CancelCommand = new DelegateCommand(CancelExecute);
            LoginCommand = new DelegateCommand(LoginExecute);
        }

        #region Command Props

        /// <summary>
        /// Команда отмены настроек
        /// </summary>
        public ICommand CancelCommand { get; private set; }

        /// <summary>
        /// Войти в учетную запись
        /// </summary>
        public ICommand LoginCommand { get; private set; }

        #endregion

        #region Command Executes

        #endregion

        private void CancelExecute()
        {
            IsOpen = false;
        }

        private void LoginExecute()
        {
            IsOpen = false;

            _userSettings.AccessToken = string.Empty;
            _userSettings.UserName = UserName;
            _userSettings.Password = PbExt.Password;
            _userSettings.Save(isSilent: false);
        }

        #endregion
    }
}
