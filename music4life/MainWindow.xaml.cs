using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();

            _viewModel.RequestOpenSongList += () =>
            {
                MainContent.Content = new SongListView();
            };

            this.DataContext = _viewModel;

            LoadAndScanMusicOnStartup();

            MainContent.Content = new SongListView();
        }
        private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel != null)
            {
                double step = e.Delta > 0 ? 2 : -2;
                _viewModel.ChangeVolume(step);
            }
            e.Handled = true;
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (_viewModel != null) _viewModel.IsDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.IsDragging = false;
                _viewModel.SeekTo(seekSlider.Value);
            }
        }

        private void seekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel != null)
            {
                if (!_viewModel.IsDragging)
                {
                    _viewModel.SeekTo(seekSlider.Value);
                }
            }
        }

        private void seekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }

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

                        if (_viewModel != null)
                        {
                            var songs = MusicManager.AllTracks;
                            _viewModel.RefreshData(songs.ToList());
                        }
                    }
                }
                catch { }
            }
        }

        private void BtnAllSongs_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SearchText = string.Empty;
                _viewModel.RefreshData(_viewModel.AllSongs.ToList());
            }

            MainContent.Content = new SongListView();
        }

        private void BtnAlbums_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.LoadAlbums();

                MainContent.Content = new AlbumView();
            }
        }

        private void BtnArtists_Click(object sender, RoutedEventArgs e)
        {
           if (_viewModel != null)
           {
               _viewModel.LoadArtists();
               MainContent.Content = new ArtistsView();
           }
        }

        private void BtnGenres_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.LoadGenres();

                MainContent.Content = new GenresView();
            }
        }

        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
{
    this.Opacity = 0.5;
    
    var createWindow = new music4life.Views.CreatePlaylistWindow();
    createWindow.Owner = this;
    
    if (createWindow.ShowDialog() == true)
    {
        string newPlaylistName = createWindow.CreatedPlaylistName;

        if (this.DataContext is MainViewModel vm)
        {
            if (vm.CreatePlaylistCommand.CanExecute(newPlaylistName))
            {
                vm.CreatePlaylistCommand.Execute(newPlaylistName);
            }
        }
    }

    this.Opacity = 1.0;
}

        private void BtnFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.ShowFavoritesCommand.CanExecute(null))
            {
                _viewModel.ShowFavoritesCommand.Execute(null);

                MainContent.Content = new SongListView();
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
        private void BtnCloseApp_Click(object sender, RoutedEventArgs e)
            => System.Windows.Application.Current.Shutdown();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => this.WindowState = WindowState.Minimized;

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
            try
            {
                this.DragMove();
            }
            catch { }
        }
    }
}