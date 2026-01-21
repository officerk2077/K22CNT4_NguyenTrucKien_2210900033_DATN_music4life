using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using music4life.Models;
using music4life.Services;
using music4life.ViewModels;
using music4life.Views;

namespace music4life
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        private object _songListViewCache;
        private object _albumViewCache;
        private object _artistViewCache;
        private object _genreViewCache;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            _viewModel.RequestOpenSongList += () => SwitchToSongList();

            LoadAndScanMusicOnStartup();

            SwitchToSongList();
        }
        private void SwitchToSongList()
        {
            if (_songListViewCache == null) _songListViewCache = new SongListView();
            MainContent.Content = _songListViewCache;
        }

        private void BtnAllSongs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchText = string.Empty;
            _viewModel.RefreshData(_viewModel.AllSongs.ToList());
            SwitchToSongList();
        }

        private void BtnAlbums_Click(object sender, RoutedEventArgs e)
        {
            if (_albumViewCache == null)
            {
                _albumViewCache = new AlbumView();
                _viewModel.LoadAlbumsAsync();
            }
            MainContent.Content = _albumViewCache;
        }

        private void BtnArtists_Click(object sender, RoutedEventArgs e)
        {
            if (_artistViewCache == null)
            {
                _artistViewCache = new ArtistsView();
                _viewModel.LoadArtistsAsync();
            }
            MainContent.Content = _artistViewCache;
        }

        private void BtnGenres_Click(object sender, RoutedEventArgs e)
        {
            if (_genreViewCache == null)
            {
                _genreViewCache = new GenresView();
                _viewModel.LoadGenresAsync();
            }
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
        private async void LoadAndScanMusicOnStartup()
        {
            if (File.Exists("settings.json"))
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync("settings.json");
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);

                    if (settings != null && settings.MusicFolders.Count > 0)
                    {
                        await MusicManager.ScanMusic(settings.MusicFolders);

                        var songs = MusicManager.AllTracks;
                        _viewModel.RefreshData(songs.ToList());
                    }
                }
                catch { }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0.5;
            SettingWindow settingWindow = new SettingWindow();
            settingWindow.Owner = this;
            settingWindow.ShowDialog();
            this.Opacity = 1.0;
        }

        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0.5;
            var createWindow = new music4life.Views.CreatePlaylistWindow();
            createWindow.Owner = this;

            if (createWindow.ShowDialog() == true)
            {
                string newPlaylistName = createWindow.CreatedPlaylistName;
                if (_viewModel.CreatePlaylistCommand.CanExecute(newPlaylistName))
                {
                    _viewModel.CreatePlaylistCommand.Execute(newPlaylistName);
                }
            }
            this.Opacity = 1.0;
        }
        private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 2 : -2;
            _viewModel.ChangeVolume(step);
            e.Handled = true;
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _viewModel.IsDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _viewModel.IsDragging = false;
            if (sender is Slider s) _viewModel.SeekTo(s.Value);
        }

        private void seekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_viewModel.IsDragging && sender is Slider s)
            {
                _viewModel.SeekTo(s.Value);
            }
        }

        private void seekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
        private void BtnCloseApp_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.MaxHeight = SystemParameters.WorkArea.Height;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            try { this.DragMove(); } catch { }
        }
    }
}