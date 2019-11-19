using System;
using System.ComponentModel;
using System.Windows;
using WallpaperDownloader.Extension;
using WallpaperDownloader.Properties;
using WallpaperDownloader.ViewModels;

namespace WallpaperDownloader.Views
{
    public partial class MainWindow
    {
        #region Members and Properties      

        private readonly object _lockedObj = new object();

        /// <summary>
        /// VM главного окна программы
        /// </summary>
        public MainViewModel ViewModel => (MainViewModel)DataContext;

        private bool _isReallyLoaded;

        #endregion

        /// <inheritdoc />
        /// <summary>
        /// Констурктор главного окна приложения
        /// </summary>
        public MainWindow()
        {
            FocusVisualStyleRemover.Init();

            InitializeComponent();
        }

        private void LoadDimensions()
        {
            if (ViewModel.UserSettings.MainWindowSettings.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            Width = Math.Max(ViewModel.UserSettings.MainWindowSettings.Width, Settings.Default.WindowMinWidth);
            Height = Math.Max(ViewModel.UserSettings.MainWindowSettings.Height, Settings.Default.WindowMinHeight);

            var left = ViewModel.UserSettings.MainWindowSettings.Left;
            if (left < SystemParameters.VirtualScreenWidth && left >= 0)
            {
                Left = left;
            }

            var top = ViewModel.UserSettings.MainWindowSettings.Top;
            if (top < SystemParameters.VirtualScreenHeight && top >= 0)
            {
                Top = top;
            }
        }

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                lock (this)
                {
                    if (WindowState != WindowState.Maximized && _isReallyLoaded)
                    {
                        ViewModel.UserSettings.MainWindowSettings.Top = Top;
                        ViewModel.UserSettings.MainWindowSettings.Left = Left;
                        ViewModel.UserSettings.MainWindowSettings.Width = ActualWidth;
                        ViewModel.UserSettings.MainWindowSettings.Height = ActualHeight;
                    }
                }
            }
            catch
            {
                //ignore    
            }
        }

        private void MainWindow_OnLocationChanged(object sender, System.EventArgs e)
        {
            if (WindowState != WindowState.Maximized && IsLoaded)
            {
                var winDim = ViewModel.UserSettings.MainWindowSettings;
                winDim.Left = Left;
                winDim.Top = Top;
            }
        }
        
        public void Open()
        {
            WindowState = ViewModel.UserSettings.MainWindowSettings.IsMaximized ? WindowState.Maximized : WindowState.Normal;
            Activate();
        }

        private void ActiveWindow()
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadDimensions();
            lock (_lockedObj)
            {
                _isReallyLoaded = true;
            }

            SizeChanged += MainWindow_OnSizeChanged;
            LocationChanged += MainWindow_OnLocationChanged;

            ViewModel.AuthorizeFromAccessToken();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (App.IsExiting)
            {
                return;
            }

            if (WindowState != WindowState.Minimized)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;

                e.Cancel = true;
            }
        }

        private void MainWindow_OnStateChanged(object sender, System.EventArgs e)
        {
            if (WindowState != WindowState.Minimized)
            {
                ShowInTaskbar = true;
            }
        }

        private void MainWindow_OnActivated(object sender, System.EventArgs e)
        {
            ShowInTaskbar = true;
        }

        private void TaskbarIcon_OnTrayLeftMouseDown(object sender, RoutedEventArgs e)
        {
            ActiveWindow();
        }
    }
}
