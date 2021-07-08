using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Logging;
using Prism.Commands;
using Prism.Mvvm;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Exception;
using VkNet.Model;
using VkNet.Model.RequestParams;
using WallpaperDownloader.AppCommon;
using WallpaperDownloader.Configuration;
using WallpaperDownloader.Data;
using WallpaperDownloader.Enums;
using WallpaperDownloader.EventArgs;
using WallpaperDownloader.Extension;
using WallpaperDownloader.Logging;
using WallpaperDownloader.UiHelpers;
using Timer = System.Timers.Timer;

namespace WallpaperDownloader.ViewModels
{
    public class MainViewModel : BindableBase
    {
        #region Members

        private VkApi _api;

        private readonly Timer _updateIsAuthorizedTimer = new Timer(5000);
        private const int MaxConnectionAttempt = 10;
        private Thread _wallpaperUpdaterThread;
        private Period _selectedPeriod;
        private DateTime _lastDownloadDateTime;
        private GroupViewModel _workGroup;
        private bool _isLoadStartupProcedure;
        private int _connectionAttempt;

        #endregion

        #region Props

        /// <summary>
        /// Пользовательские настройки приложения
        /// </summary>
        public UserSettings UserSettings { get; }

        /// <summary>
        /// Минимальная ширина
        /// </summary>
        public int WindowMinWidth { get; }

        /// <summary>
        /// Минимальная высота
        /// </summary>
        public int WindowMinHeight { get; }

        /// <summary>
        /// Список полученных ошибок
        /// </summary>
        public ObservableCollection<string> ErrorCollection { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Вью модель настроек
        /// </summary>
        public ConnectionSetupViewModel ConnectionSetupViewModel { get; set; }

        /// <summary>
        /// Вью модель ввода кода двухфакторной авторизации
        /// </summary>
        public TwoFactorAuthorizationViewModel TwoFactorAuthorizationViewModel { get; set; }

        /// <summary>
        /// Вью модель ввода каптчи
        /// </summary>
        public CaptchaViewModel CaptchaViewModel { get; set; }

        /// <summary>
        /// Статус авторизации
        /// </summary>
        public bool AuthorizationStatus => _api.IsAuthorized;

        public ObservableCollection<Period> Periods { get; } = new ObservableCollection<Period>
        {
            new Period(WallpaperUpdateType.HalfAnHour, Localization.strings.Period1, new TimeSpan(0,0,30,0)),
            new Period(WallpaperUpdateType.OneHour, Localization.strings.Period2, new TimeSpan(0,1,0,0)),
            new Period(WallpaperUpdateType.TwoHour, Localization.strings.Period3, new TimeSpan(0,2,0,0))
        };

        public Period SelectedPeriod
        {
            get => _selectedPeriod;
            set => SetProperty(ref _selectedPeriod, value);
        }

        public DateTime LastDownloadDateTime
        {
            get => _lastDownloadDateTime;
            set => SetProperty(ref _lastDownloadDateTime, value);
        }

        public GroupViewModel WorkGroup
        {
            get => _workGroup;
            set => SetProperty(ref _workGroup, value);
        }

        #endregion

        public MainViewModel()
        {
            //test
            LoggerFacade.LogMessageAdded += LoggerFacade_OnLogMessageAdded;

            UserSettings = new UserSettings();
            UserSettings.Load();

            SelectedPeriod = Periods.FirstOrDefault(p => p.Type == UserSettings.WallpaperUpdateType);
            LastDownloadDateTime = UserSettings.LastDownloadPhotoDateTime;

            WindowMinHeight = Properties.Settings.Default.WindowMinHeight;
            WindowMinWidth = Properties.Settings.Default.WindowMinWidth;

            UserSettings.Saved += Settings_OnSaved;

            InitApi();
            InitViewModels();

            InitCommands();

            _updateIsAuthorizedTimer.Elapsed += UpdateIsAuthorizedTimer_OnElapsed;
            _updateIsAuthorizedTimer.Start();
        }

        private void Updater()
        {
            while (true)
            {
                Update();
                Thread.Sleep(SelectedPeriod.TimeSpanPeriod);
            }
        }

        private void Update()
        {
            try
            {
                if (AuthorizationStatus)
                {
                    try
                    {
                        //Проверяем истек ли токен
                        _api.Stats.TrackVisitor();
                    }
                    catch (UserAuthorizationFailException e)
                    {
                        //Истек токен
                        LoggerFacade.WriteInformation(e.Message);
                        AuthorizeFromLogPass(captchaSid: null, captchaKey: null, isOpenTwoFactorWindow: false);
                    }

                    var wall = _api.Wall.Get(new WallGetParams
                    {
                        OwnerId = AppInfo.HDWallpaperGroupId
                    });

                    var post = wall.WallPosts.FirstOrDefault();
                    var photoAttachment = post?.Attachment;

                    if (photoAttachment != null && photoAttachment.Type.Name == "Photo")
                    {
                        var photo = (VkNet.Model.Attachments.Photo)photoAttachment.Instance;

                        var isHorizontal = true;

                        var size = photo?.Sizes.FirstOrDefault();
                        if (size != null && size?.Height > size?.Width)
                        {
                            isHorizontal = false;
                        }

                        if (photo?.Id != null && isHorizontal)
                        {
                            if (UserSettings.LastDownloadPhotoId != photo.Id.ToString())
                            {
                                var comments = _api.Photo.GetComments(new PhotoGetCommentsParams
                                {
                                    OwnerId = photo.OwnerId,
                                    PhotoId = (ulong)photo.Id,
                                    Extended = true
                                });

                                var comment = comments?.FirstOrDefault();

                                var docAttachment = comment?.Attachment;
                                if (docAttachment != null && docAttachment.Type.Name == "Document")
                                {
                                    var document = (VkNet.Model.Attachments.Document)docAttachment.Instance;
                                    var url = document?.Uri;

                                    if (!string.IsNullOrWhiteSpace(url))
                                    {
                                        Directory.CreateDirectory(PathHelper.PhotoFolderPath);

                                        var isOk = SavePhoto(url);

                                        if (isOk)
                                        {
                                            ExtensionMethods.SetBackgroud(PathHelper.PhotoFilePath);
                                        }
                                    }
                                }

                                LastDownloadDateTime = DateTime.Now;

                                UserSettings.LastDownloadPhotoId = photo.Id.ToString();
                                UserSettings.LastDownloadPhotoDateTime = LastDownloadDateTime;
                                UserSettings.Save(isSilent: true);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LoggerFacade.WriteError(e);
            }
        }

        private static bool SavePhoto(string url)
        {
            var isOk = true;

            try
            {
                const int bytesToRead = 100;

                var request = WebRequest.Create(new Uri(url));
                request.Timeout = -1;
                var response = request.GetResponse();
                var responseStream = response.GetResponseStream();
                var reader = new BinaryReader(responseStream);
                var memoryStream = new MemoryStream();

                var bytebuffer = new byte[bytesToRead];
                var bytesRead = reader.Read(bytebuffer, 0, bytesToRead);

                while (bytesRead > 0)
                {
                    memoryStream.Write(bytebuffer, 0, bytesRead);
                    bytesRead = reader.Read(bytebuffer, 0, bytesToRead);
                }

                var fs = new FileStream(PathHelper.PhotoFilePath, FileMode.Create);

                memoryStream.WriteTo(fs);

                fs.Close();
                memoryStream.Close();
            }
            catch (Exception e)
            {
                LoggerFacade.WriteError(e);
                isOk = false;
            }

            return isOk;
        }

        #region Initialize

        /// <summary>
        /// Инициализация апи
        /// </summary>
        private void InitApi()
        {
            if (_api != null)
            {
                _api.OnTokenExpires -= Api_OnTokenExpires;
            }

            _api = new VkApi();

            _api.OnTokenExpires += Api_OnTokenExpires;
        }

        /// <summary>
        /// Инициализация вью моделей
        /// </summary>
        private void InitViewModels()
        {
            ConnectionSetupViewModel = new ConnectionSetupViewModel(UserSettings);
            TwoFactorAuthorizationViewModel = new TwoFactorAuthorizationViewModel();
            CaptchaViewModel = new CaptchaViewModel();
            CaptchaViewModel.CaptchaEntered += CaptchaViewModel_OnCaptchaEntered;
        }

        #endregion

        #region Authorize

        /// <summary>
        /// Авторизация по токену
        /// </summary>
        public async void AuthorizeFromAccessToken(ulong? captchaSid = null, string captchaKey = null)
        {
            if (string.IsNullOrWhiteSpace(UserSettings.AccessToken))
                return;

            try
            {
                var apiParams = new ApiAuthParams
                {
                    ApplicationId = AppInfo.VkAppId,
                    AccessToken = UserSettings.AccessToken,
                    CaptchaKey = captchaKey,
                    Settings = Settings.All
                };

                if (captchaSid != null)
                {
                    apiParams.CaptchaSid = captchaSid;
                }

                await _api.AuthorizeAsync(apiParams);
            }
            catch (CaptchaNeededException e)
            {
                InitApi();

                CaptchaViewModel.Open(e.Sid, e.Img);
                return;
            }
            catch (Exception e)
            {
                InitApi();

                LoggerFacade.WriteError(Localization.strings.AuthorizeError, e, isShow: true);
                ConnectionSettingsCommand.Execute(null);
            }

            RaisePropertyChanged(nameof(AuthorizationStatus));

            if (AuthorizationStatus)
            {
                try
                {
                    //Включаем данные о посещениях
                    await _api.Stats.TrackVisitorAsync();
                    OnLoad();
                }
                catch (UserAuthorizationFailException e)
                {
                    //Истек токен
                    LoggerFacade.WriteInformation(e.Message);
                    AuthorizeFromLogPass(captchaSid: null, captchaKey: null, isOpenTwoFactorWindow: false);
                }
            }
        }

        /// <summary>
        /// Авторизация по логину и паролю
        /// </summary>
        private void AuthorizeFromLogPass(ulong? captchaSid = null, string captchaKey = null, bool isOpenTwoFactorWindow = true)
        {
            if (string.IsNullOrWhiteSpace(UserSettings.UserName) || UserSettings.Password.Length == 0)
                return;

            if (isOpenTwoFactorWindow)
            {
                UiInvoker.Invoke(() =>
                {
                    TwoFactorAuthorizationViewModel.Open();
                });
            }

            Task.Run(() =>
            {
                try
                {
                    var apiParams = new ApiAuthParams
                    {
                        ApplicationId = AppInfo.VkAppId,
                        Login = UserSettings.UserName,
                        Password = new NetworkCredential(string.Empty, UserSettings.Password).Password,
                        TwoFactorAuthorization = () =>
                        {
                            var code = TwoFactorAuthorizationViewModel.GetCode();
                            return code;
                        },
                        CaptchaKey = captchaKey,
                        Settings = Settings.All,
                        IsTokenUpdateAutomatically = true
                    };

                    if (captchaSid != null)
                    {
                        apiParams.CaptchaSid = captchaSid;
                    }

                    _api.Authorize(apiParams);

                    if (_api.IsAuthorized)
                    {
                        UserSettings.AccessToken = _api.Token;
                        if (_api.UserId != null) UserSettings.UserId = (uint)_api.UserId;
                        UserSettings.Save();
                    }
                }
                catch (CaptchaNeededException e)
                {
                    UiInvoker.Invoke(() =>
                    {
                        CaptchaViewModel.Open(e.Sid, e.Img);
                    });
                }
                catch (VkAuthorizationException e)
                {
                    LoggerFacade.WriteError(Localization.strings.AuthorizeError + Environment.NewLine + e.Message,
                        isShow: true);

                    UiInvoker.Invoke(() =>
                    {
                        ConnectionSettingsCommand.Execute(null);
                    });
                }
                catch (Exception e)
                {
                    LoggerFacade.WriteError(Localization.strings.AuthorizeError + Environment.NewLine + e.Message,
                        isShow: true);
                }
                finally
                {
                    if (!_api.IsAuthorized)
                    {
                        //TODO Костыль
                        InitApi();
                    }

                    UiInvoker.Invoke(() =>
                    {
                        TwoFactorAuthorizationViewModel.IsOpen = false;

                        RaisePropertyChanged(nameof(AuthorizationStatus));

                        if (AuthorizationStatus)
                        {
                            OnLoad();
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Стартовые процедуры
        /// </summary>
        private async void StartupProcedure()
        {
            _isLoadStartupProcedure = true;

            if (_wallpaperUpdaterThread == null)
            {
                _wallpaperUpdaterThread = new Thread(Updater);
                _wallpaperUpdaterThread.Start();
            }

            try
            {
                var groupIdInStr = (AppInfo.HDWallpaperGroupId * -1).ToString();

                var groups = await _api.Groups.GetByIdAsync(new List<string>{groupIdInStr}, groupIdInStr,GroupsFields.All);

                if (groups != null && groups.Count != 0)
                {
                    WorkGroup = new GroupViewModel(groups.First());
                }
            }
            catch (Exception e)
            {
                LoggerFacade.WriteError(e);
            }
        }

        #endregion

        #region Others

        /// <summary>
        /// Загрузка
        /// </summary>
        private void OnLoad()
        {
            if (!_isLoadStartupProcedure)
            {
                StartupProcedure();
            }
        }

        public void OnUnload()
        {
            UserSettings.Save();
        }

        private void ExitFromApp()
        {
            try
            {
                _wallpaperUpdaterThread?.Abort();
            }
            catch
            {
                // ignored
            }

            App.Exit();
        }

        #endregion

        #region Event Handlers

        #region Settings

        private void Settings_OnSaved(object sender, SettingsSavedEventArgs e)
        {
            if (e.IsChanged(nameof(UserSettings.UserName)) || e.IsChanged(nameof(UserSettings.Password)) || string.IsNullOrWhiteSpace(UserSettings.AccessToken))
            {
                AuthorizeFromLogPass();
            }
        }

        #endregion

        #region Logger

        private void LoggerFacade_OnLogMessageAdded(object sender, LogMessageAddedEventArgs e)
        {
            UiInvoker.BeginInvoke(() =>
            {
                try
                {
                    ErrorCollection.Add(e.Message);

                    if (e.IsShow)
                    {
                        MessageBoxExt.Show(Localization.strings.MessageBoxError, e.Message);
                    }
                }
                catch
                {
                    //Ignore
                }

            });
        }

        #endregion

        #region Captcha

        private void CaptchaViewModel_OnCaptchaEntered(object sender, CaptchaEnteredEventArgs e)
        {
            AuthorizeFromLogPass(e.CaptchaSid, e.CaptchaKey);
        }

        #endregion

        #region Timers

        private void UpdateIsAuthorizedTimer_OnElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_api.IsAuthorized)
            {
                if (_connectionAttempt == MaxConnectionAttempt)
                {
                    if (!ConnectionSetupViewModel.IsOpen)
                    {
                        UiInvoker.Invoke(() =>
                        {
                            ConnectionSettingsCommand.Execute(null);
                            _connectionAttempt = 0;
                        });
                    }
                    return;
                }

                _connectionAttempt++;
            }
            else
            {
                _connectionAttempt = 0;
            }
        }

        #endregion

        #region Api

        private void Api_OnTokenExpires(VkApi sender)
        {
            AuthorizeFromLogPass(captchaSid: null, captchaKey: null, isOpenTwoFactorWindow: false);
        }

        #endregion

        #endregion

        #region Commands

        private void InitCommands()
        {
            ConnectionSettingsCommand = new DelegateCommand(ConnectionSettingsExecute);
            ExitFromAppCommand = new DelegateCommand(ExitFromAppExecute);
            UpdateWallpaperCommand = new DelegateCommand(UpdateWallpaperExecute);
        }

        #region Command Props

        public ICommand ConnectionSettingsCommand { get; private set; }

        public ICommand ExitFromAppCommand { get; private set; }

        public ICommand UpdateWallpaperCommand { get; private set; }

        #endregion

        private void ConnectionSettingsExecute()
        {
            ConnectionSetupViewModel.OpenConnectionSettings();
        }

        private void ExitFromAppExecute()
        {
            ExitFromApp();
        }

        private void UpdateWallpaperExecute()
        {
            Update();
        }

        #endregion
    }
}

