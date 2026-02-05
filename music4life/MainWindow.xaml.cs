using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using music4life.Models;
using music4life.Services;
using music4life.ViewModels;
using music4life.Views;

using WinForms = System.Windows.Forms;

namespace music4life
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        private WinForms.NotifyIcon _notifyIcon;
        private bool _isMinimizeToTrayEnabled = false;

        private SongListView _songListViewCache;
        private AlbumView _albumViewCache;
        private ArtistsView _artistViewCache;
        private GenresView _genreViewCache;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            _viewModel.RequestOpenSongList += () => SwitchToSongList();

            InitializeTrayIcon();

            InitializeAppStartup();

            SwitchToSongList();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon();
            _notifyIcon.Text = "Music4Life"; 
            _notifyIcon.Visible = false;    

            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly().Location);
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    this.Show(); 
                    this.WindowState = WindowState.Normal; 
                    _notifyIcon.Visible = false; 
                    this.Activate(); 
                }
            };
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _isMinimizeToTrayEnabled)
            {
                this.Hide();
                _notifyIcon.Visible = true;
            }

            base.OnStateChanged(e);
        }
        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }

        private async void InitializeAppStartup()
        {
            _viewModel.LoadSongsFromDb();

            await LoadAndScanMusicOnStartup();
        }

        private void SwitchToSongList()
        {
            if (_songListViewCache == null) _songListViewCache = new SongListView();
            MainContent.Content = _songListViewCache;
        }

        private void BtnAllSongs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchText = string.Empty;
            if (_viewModel.AllSongs != null)
            {
                _viewModel.RefreshData(_viewModel.AllSongs.ToList());
            }
            SwitchToSongList();
        }

        private void BtnAlbums_Click(object sender, RoutedEventArgs e)
        {
            if (_albumViewCache == null) _albumViewCache = new AlbumView();
            _viewModel.LoadAlbumsAsync();
            MainContent.Content = _albumViewCache;
        }

        private void BtnArtists_Click(object sender, RoutedEventArgs e)
        {
            if (_artistViewCache == null) _artistViewCache = new ArtistsView();
            _viewModel.LoadArtistsAsync();
            MainContent.Content = _artistViewCache;
        }

        private void BtnGenres_Click(object sender, RoutedEventArgs e)
        {
            if (_genreViewCache == null) _genreViewCache = new GenresView();
            _viewModel.LoadGenresAsync();
            MainContent.Content = _genreViewCache;
        }

        private void BtnFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ShowFavoritesCommand.CanExecute(null))
            {
                _viewModel.ShowFavoritesCommand.Execute(null);
                SwitchToSongList();
            }
        }

        private async Task LoadAndScanMusicOnStartup()
        {
            var folders = new List<string>();

            string roamingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "music4life");
            string settingsPath = Path.Combine(roamingFolder, "settings.json");

            if (File.Exists(settingsPath))
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    if (settings != null)
                    {
                        if (settings.MusicFolders != null) folders.AddRange(settings.MusicFolders);

                        _isMinimizeToTrayEnabled = settings.IsMinimizeToTrayEnabled;
                    }
                }
                catch { }
            }

            if (folders.Count == 0)
            {
                string defaultMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                if (Directory.Exists(defaultMusic)) folders.Add(defaultMusic);
            }

            if (folders.Count > 0)
            {
                await MusicManager.ScanMusic(folders);

                this.Dispatcher.Invoke(() =>
                {
                    if (MusicManager.AllTracks != null)
                    {
                        _viewModel.RefreshData(MusicManager.AllTracks.ToList());
                    }
                });
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0.5;
            SettingWindow settingWindow = new SettingWindow { Owner = this };
            settingWindow.ShowDialog();
            this.Opacity = 1.0;

            _ = LoadAndScanMusicOnStartup();
        }

        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0.5;
            var createWindow = new CreatePlaylistWindow { Owner = this };
            if (createWindow.ShowDialog() == true)
            {
                string name = createWindow.CreatedPlaylistName;
                if (_viewModel.CreatePlaylistCommand.CanExecute(name))
                {
                    _viewModel.CreatePlaylistCommand.Execute(name);

                    ShowToast($"Đã tạo playlist '{name}' thành công!");
                }
            }
            this.Opacity = 1.0;
        }

        private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _viewModel.ChangeVolume(e.Delta > 0 ? 2 : -2);
            e.Handled = true;
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) => _viewModel.IsDragging = true;

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _viewModel.IsDragging = false;
            if (sender is Slider s) _viewModel.SeekTo(s.Value);
        }

        private void seekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_viewModel.IsDragging && sender is Slider s) _viewModel.SeekTo(s.Value);
        }

        private void BtnCloseApp_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.MaxHeight = SystemParameters.WorkArea.Height;
                this.WindowState = WindowState.Maximized;
            }
            else this.WindowState = WindowState.Normal;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            try { this.DragMove(); } catch { }
        }

        public async void ShowToast(string message)
        {
            Window window = new Window()
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Opacity = 0
            };

            Border border = new Border()
            {
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DD222222")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 15, 20, 15)
            };

            TextBlock textBlock = new TextBlock()
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            border.Child = textBlock;
            window.Content = border;
            window.Opacity = 1;

            window.Show();

            await Task.Delay(1500);
            window.Close();
        }
    }
}