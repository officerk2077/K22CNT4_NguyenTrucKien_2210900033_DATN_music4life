using music4life.Models;
using music4life.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace music4life.ViewModels
{
    public class AlbumInfo { public string Title { get; set; } public string Artist { get; set; } public int SongCount { get; set; } public ImageSource AlbumCover { get; set; } }
    public class ArtistInfo { public string Name { get; set; } public int SongCount { get; set; } public string SongCountText => $"{SongCount} bài hát"; public ImageSource ArtistImage { get; set; } }
    public class GenreInfo { public string Name { get; set; } public int SongCount { get; set; } public string SongCountText => $"{SongCount} bài hát"; }

    public class MainViewModel : BaseViewModel
    {
        public event Action RequestOpenSongList;
        private bool _isViewingFavorites = false;
        private Song _lastPlayingSong;
        private string _currentSortType = "";

        private Song _songInSidebar;
        public Song SongInSidebar { get => _songInSidebar; set { _songInSidebar = value; OnPropertyChanged(); } }

        private bool _isInfoFavorite;
        public bool IsInfoFavorite { get => _isInfoFavorite; set { _isInfoFavorite = value; OnPropertyChanged(); } }

        private ObservableCollection<Playlist> _userPlaylists;
        public ObservableCollection<Playlist> UserPlaylists { get => _userPlaylists; set { _userPlaylists = value; OnPropertyChanged(); } }

        private Song _selectedSong;
        public Song SelectedSong
        {
            get => _selectedSong;
            set
            {
                _selectedSong = value;
                OnPropertyChanged();
                if (_selectedSong != null) UpdateSongInfoDisplay(_selectedSong);
            }
        }
        private ObservableCollection<GenreInfo> _genreList; public ObservableCollection<GenreInfo> GenreList { get => _genreList; set { _genreList = value; OnPropertyChanged(); } }
        private ObservableCollection<ArtistInfo> _artistList; public ObservableCollection<ArtistInfo> ArtistList { get => _artistList; set { _artistList = value; OnPropertyChanged(); } }
        private ObservableCollection<AlbumInfo> _albumList; public ObservableCollection<AlbumInfo> AlbumList { get => _albumList; set { _albumList = value; OnPropertyChanged(); } }

        private Song _currentSong;
        public Song CurrentSong { get => _currentSong; set { _currentSong = value; OnPropertyChanged(); } }

        private ObservableCollection<Song> _allSongs = new ObservableCollection<Song>();
        public ObservableCollection<Song> AllSongs { get => _allSongs; set { _allSongs = value; OnPropertyChanged(); } }

        private ObservableCollection<Song> _displayedTracks;
        public ObservableCollection<Song> DisplayedTracks { get => _displayedTracks; set { _displayedTracks = value; OnPropertyChanged(); } }

        private int _totalSongs;
        public int TotalSongs { get => _totalSongs; set { _totalSongs = value; OnPropertyChanged(); } }

        private string _searchText;
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); FilterSongs(); } }

        private bool _isPlaying; public bool IsPlaying { get => _isPlaying; set { _isPlaying = value; OnPropertyChanged(); } }
        private string _songTitle = "Music for Life"; public string SongTitle { get => _songTitle; set { _songTitle = value; OnPropertyChanged(); } }
        private string _songArtist = "Select a song to play"; public string SongArtist { get => _songArtist; set { _songArtist = value; OnPropertyChanged(); } }
        private ImageSource _songCover; public ImageSource SongCover { get => _songCover; set { _songCover = value; OnPropertyChanged(); } }
        private string _currentTime = "00:00"; public string CurrentTime { get => _currentTime; set { _currentTime = value; OnPropertyChanged(); } }
        private string _remainingTime = "00:00"; public string RemainingTime { get => _remainingTime; set { _remainingTime = value; OnPropertyChanged(); } }

        private double _sliderValue;
        public double SliderValue { get => _sliderValue; set { if (_sliderValue != value) { _sliderValue = value; OnPropertyChanged(); if (IsDragging) { TimeSpan currentTs = TimeSpan.FromSeconds(_sliderValue); CurrentTime = currentTs.ToString(@"mm\:ss"); TimeSpan totalTs = TimeSpan.FromSeconds(SliderMaximum); TimeSpan remainingTs = totalTs - currentTs; if (remainingTs.TotalSeconds < 0) remainingTs = TimeSpan.Zero; RemainingTime = remainingTs.ToString(@"mm\:ss"); } } } }

        private double _sliderMaximum = 100; public double SliderMaximum { get => _sliderMaximum; set { _sliderMaximum = value; OnPropertyChanged(); } }
        private double _volume = 50; public double Volume { get => _volume; set { _volume = value; OnPropertyChanged(); MusicPlayer.SetVolume(value); } }
        public bool IsDragging { get; set; } = false;
        private bool _isShuffle; public bool IsShuffle { get => _isShuffle; set { _isShuffle = value; OnPropertyChanged(); } }
        private string _currentRepeatMode = "None"; public string CurrentRepeatMode { get => _currentRepeatMode; set { _currentRepeatMode = value; OnPropertyChanged(); } }
        public ObservableCollection<Song> PlayingQueue { get; set; } = new ObservableCollection<Song>();
        private string _songAlbum; public string SongAlbum { get => _songAlbum; set { _songAlbum = value; OnPropertyChanged(); } }
        private string _songYear; public string SongYear { get => _songYear; set { _songYear = value; OnPropertyChanged(); } }
        private string _songTechInfo; public string SongTechInfo { get => _songTechInfo; set { _songTechInfo = value; OnPropertyChanged(); } }

        public ICommand PlayPauseCommand { get; set; }
        public ICommand NextCommand { get; set; }
        public ICommand PreviousCommand { get; set; }
        public ICommand ShuffleCommand { get; set; }
        public ICommand ToggleRepeatCommand { get; set; }
        public ICommand PlaySongCommand { get; set; }
        public ICommand PlayNextCommand { get; set; }
        public ICommand AddToQueueCommand { get; set; }
        public ICommand RemoveSongCommand { get; set; }
        public ICommand ToggleFavoriteCommand { get; set; }
        public ICommand ShowFavoritesCommand { get; set; }
        public ICommand RemoveFromQueueCommand { get; set; }
        public ICommand OpenAlbumCommand { get; set; }
        public ICommand OpenGenreCommand { get; set; }
        public ICommand CreatePlaylistCommand { get; set; }
        public ICommand AddSongToPlaylistCommand { get; set; }
        public ICommand OpenPlaylistCommand { get; set; }

        public ICommand DeletePlaylistCommand { get; set; }

        public MainViewModel()
        {
            DisplayedTracks = new ObservableCollection<Song>();
            MusicPlayer.SongChanged += OnSongChanged;
            MusicPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
            MusicPlayer.PositionChanged += OnPositionChanged;

            UserPlaylists = PlaylistService.AllPlaylists;

            InitializeCommands();
        }

        private void UpdateSongInfoDisplay(Song song)
        {
            if (song == null) return;
            SongInSidebar = song;
            SongTitle = song.Title;
            SongArtist = song.Artist;
            SongAlbum = song.Album;
            SongYear = !string.IsNullOrEmpty(song.Year) ? song.Year : "";
            SongTechInfo = song.TechnicalInfo;
            SongCover = GetSongCover(song.FilePath);
            IsInfoFavorite = song.IsFavorite;
        }

        private ImageSource GetSongCover(string filePath)
        {
            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    var pic = file.Tag.Pictures.FirstOrDefault();
                    if (pic != null)
                    {
                        var ms = new MemoryStream(pic.Data.Data);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit(); bitmap.StreamSource = ms; bitmap.DecodePixelWidth = 200; bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
            catch { }
            return null;
        }

        private void OnSongChanged()
        {
            var song = MusicPlayer.CurrentTrack;
            CurrentSong = song;
            if (song != null)
            {
                UpdateSongInfoDisplay(song);
                if (_lastPlayingSong != null) _lastPlayingSong.IsPlaying = false;
                song.IsPlaying = true;
                _lastPlayingSong = song;
            }
            UpdatePlayingQueue();
        }

        private void OnPlaybackStateChanged(bool isPlaying) { IsPlaying = isPlaying; }

        private void OnPositionChanged(TimeSpan current, TimeSpan total)
        {
            if (!IsDragging) { SliderMaximum = total.TotalSeconds; SliderValue = current.TotalSeconds; CurrentTime = current.ToString(@"mm\:ss"); RemainingTime = (total - current).ToString(@"mm\:ss"); }
        }

        private void UpdatePlayingQueue()
        {
            if (MusicPlayer.CurrentPlaylist == null) return;
            var currentSong = MusicPlayer.CurrentTrack;
            PlayingQueue.Clear();
            if (currentSong == null) return;
            int index = MusicPlayer.CurrentPlaylist.IndexOf(currentSong);
            if (index >= 0) { for (int i = index; i < MusicPlayer.CurrentPlaylist.Count; i++) PlayingQueue.Add(MusicPlayer.CurrentPlaylist[i]); for (int i = 0; i < index; i++) PlayingQueue.Add(MusicPlayer.CurrentPlaylist[i]); }
            else { foreach (var s in MusicPlayer.CurrentPlaylist) PlayingQueue.Add(s); }
        }

        private void InitializeCommands()
        {
            PlayPauseCommand = new RelayCommand<object>((p) => { MusicPlayer.TogglePlayPause(); });
            NextCommand = new RelayCommand<object>((p) => MusicPlayer.Next());
            PreviousCommand = new RelayCommand<object>((p) => MusicPlayer.Previous());
            ShuffleCommand = new RelayCommand<object>((p) => { MusicPlayer.ToggleShuffle(); IsShuffle = MusicPlayer.IsShuffle; UpdatePlayingQueue(); });
            ToggleRepeatCommand = new RelayCommand<object>((p) => {
                if (MusicPlayer.CurrentRepeatMode == RepeatMode.None) { MusicPlayer.CurrentRepeatMode = RepeatMode.RepeatAll; CurrentRepeatMode = "RepeatAll"; }
                else if (MusicPlayer.CurrentRepeatMode == RepeatMode.RepeatAll) { MusicPlayer.CurrentRepeatMode = RepeatMode.RepeatOne; CurrentRepeatMode = "RepeatOne"; }
                else { MusicPlayer.CurrentRepeatMode = RepeatMode.None; CurrentRepeatMode = "None"; }
            });
            PlaySongCommand = new RelayCommand<Song>((song) => { if (song != null) MusicPlayer.PlayTrack(song, DisplayedTracks.ToList()); });
            PlayNextCommand = new RelayCommand<Song>((song) => {
                if (song != null && MusicPlayer.CurrentPlaylist != null)
                {
                    var index = MusicPlayer.CurrentPlaylist.IndexOf(MusicPlayer.CurrentTrack);
                    if (index >= 0 && index < MusicPlayer.CurrentPlaylist.Count - 1) MusicPlayer.CurrentPlaylist.Insert(index + 1, song);
                    else MusicPlayer.CurrentPlaylist.Add(song);
                    UpdatePlayingQueue();
                }
            });
            AddToQueueCommand = new RelayCommand<Song>((song) => { if (song != null) { MusicPlayer.CurrentPlaylist?.Add(song); UpdatePlayingQueue(); } });
            RemoveSongCommand = new RelayCommand<Song>((song) => { if (song != null) { AllSongs.Remove(song); DisplayedTracks.Remove(song); TotalSongs = DisplayedTracks.Count; } });

            ToggleFavoriteCommand = new RelayCommand<object>((param) => {
                var song = (param as Song) ?? SongInSidebar;
                if (song != null)
                {
                    song.IsFavorite = !song.IsFavorite;
                    if (song.IsFavorite) FavoriteService.Add(song.FilePath);
                    else
                    {
                        FavoriteService.Remove(song.FilePath);
                        if (_isViewingFavorites) { DisplayedTracks.Remove(song); TotalSongs = DisplayedTracks.Count; }
                    }
                    if (song == SongInSidebar) IsInfoFavorite = song.IsFavorite;
                }
            });

            ShowFavoritesCommand = new RelayCommand<object>((p) => { _isViewingFavorites = true; var favList = AllSongs.Where(s => s.IsFavorite).ToList(); DisplayedTracks = new ObservableCollection<Song>(favList); TotalSongs = DisplayedTracks.Count; });
            RemoveFromQueueCommand = new RelayCommand<Song>((song) => { if (song != null) { PlayingQueue.Remove(song); } });
            OpenAlbumCommand = new RelayCommand<string>((albumName) => { FilterSongsByAlbum(albumName); RequestOpenSongList?.Invoke(); });
            OpenGenreCommand = new RelayCommand<string>((genreName) => { FilterSongsByGenre(genreName); RequestOpenSongList?.Invoke(); });
            CreatePlaylistCommand = new RelayCommand<string>((name) => { if (!string.IsNullOrEmpty(name)) { PlaylistService.CreatePlaylist(name); UserPlaylists = PlaylistService.AllPlaylists; } });

            AddSongToPlaylistCommand = new RelayCommand<Playlist>((targetPlaylist) => {
                if (targetPlaylist != null && SelectedSong != null) { PlaylistService.AddSongToPlaylist(targetPlaylist, SelectedSong.FilePath); System.Windows.MessageBox.Show($"Đã thêm '{SelectedSong.Title}' vào playlist '{targetPlaylist.Name}'", "Thành công"); }
                else if (SelectedSong == null) { System.Windows.MessageBox.Show("Vui lòng chọn một bài hát trước!", "Thông báo"); }
            });

            OpenPlaylistCommand = new RelayCommand<Playlist>((playlist) => {
                if (playlist == null) return;
                var playlistSongs = AllSongs.Where(s => playlist.SongPaths.Contains(s.FilePath)).ToList();
                DisplayedTracks = new ObservableCollection<Song>(playlistSongs); TotalSongs = DisplayedTracks.Count; RequestOpenSongList?.Invoke();
            });

            DeletePlaylistCommand = new RelayCommand<Playlist>((playlist) =>
            {
                if (playlist == null) return;

                var result = System.Windows.MessageBox.Show(
                    $"Bạn có chắc muốn xóa playlist '{playlist.Name}' không?",
                    "Xác nhận xóa",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    PlaylistService.DeletePlaylist(playlist);
                    UserPlaylists = PlaylistService.AllPlaylists;
                }
            });
        }

        public void RefreshData(List<Song> newSongs) { if (newSongs == null) return; _isViewingFavorites = false; foreach (var song in newSongs) { if (FavoriteService.IsFavorite(song.FilePath)) song.IsFavorite = true; } AllSongs = new ObservableCollection<Song>(newSongs); DisplayedTracks = new ObservableCollection<Song>(newSongs); TotalSongs = DisplayedTracks.Count; LoadAlbums(); LoadGenres(); if (!string.IsNullOrEmpty(_currentSortType)) ApplySort(_currentSortType); }
        public void LoadArtists() { var groups = AllSongs.GroupBy(s => s.Artist).Select(g => { var firstSong = g.FirstOrDefault(); ImageSource avatar = null; if (firstSong != null) avatar = GetSongCover(firstSong.FilePath); return new ArtistInfo { Name = g.Key, SongCount = g.Count(), ArtistImage = avatar }; }).OrderBy(a => a.Name).ToList(); ArtistList = new ObservableCollection<ArtistInfo>(groups); }
        public void FilterSongsByArtist(string artistName) { _isViewingFavorites = false; var filtered = AllSongs.Where(s => s.Artist == artistName).ToList(); DisplayedTracks = new ObservableCollection<Song>(filtered); TotalSongs = DisplayedTracks.Count; if (!string.IsNullOrEmpty(_currentSortType)) ApplySort(_currentSortType); }
        public void LoadAlbums() { if (AllSongs == null || AllSongs.Count == 0) return; var groups = AllSongs.GroupBy(s => s.Album).Select(g => { var firstSong = g.FirstOrDefault(); ImageSource cover = null; string artistName = "Unknown Artist"; if (firstSong != null) { cover = GetSongCover(firstSong.FilePath); artistName = firstSong.Artist; } string albumTitle = string.IsNullOrWhiteSpace(g.Key) ? "Unknown Album" : g.Key; return new AlbumInfo { Title = albumTitle, Artist = artistName, SongCount = g.Count(), AlbumCover = cover }; }).OrderBy(a => a.Title).ToList(); AlbumList = new ObservableCollection<AlbumInfo>(groups); }
        public void FilterSongsByAlbum(string albumName) { _isViewingFavorites = false; var filtered = AllSongs.Where(s => (string.IsNullOrWhiteSpace(albumName) && string.IsNullOrWhiteSpace(s.Album)) || s.Album == albumName).ToList(); DisplayedTracks = new ObservableCollection<Song>(filtered); TotalSongs = DisplayedTracks.Count; if (!string.IsNullOrEmpty(_currentSortType)) ApplySort(_currentSortType); }
        public void LoadGenres() { if (AllSongs == null || AllSongs.Count == 0) return; var groups = AllSongs.GroupBy(s => s.Genre).Select(g => { string genreName = string.IsNullOrWhiteSpace(g.Key) ? "Unknown Genre" : g.Key; return new GenreInfo { Name = genreName, SongCount = g.Count() }; }).OrderBy(g => g.Name).ToList(); GenreList = new ObservableCollection<GenreInfo>(groups); }
        public void FilterSongsByGenre(string genreName) { _isViewingFavorites = false; var filtered = AllSongs.Where(s => (string.IsNullOrWhiteSpace(genreName) && string.IsNullOrWhiteSpace(s.Genre)) || s.Genre == genreName).ToList(); DisplayedTracks = new ObservableCollection<Song>(filtered); TotalSongs = DisplayedTracks.Count; if (!string.IsNullOrEmpty(_currentSortType)) ApplySort(_currentSortType); }
        private void FilterSongs() { _isViewingFavorites = false; if (string.IsNullOrWhiteSpace(SearchText)) { if (DisplayedTracks.Count != AllSongs.Count) { DisplayedTracks = new ObservableCollection<Song>(AllSongs); TotalSongs = DisplayedTracks.Count; if (!string.IsNullOrEmpty(_currentSortType)) ApplySort(_currentSortType); } return; } var lowerText = SearchText.ToLower(); var filtered = AllSongs.Where(s => (s.Title != null && s.Title.ToLower().Contains(lowerText)) || (s.Artist != null && s.Artist.ToLower().Contains(lowerText))).ToList(); DisplayedTracks = new ObservableCollection<Song>(filtered); TotalSongs = DisplayedTracks.Count; }
        public void ApplySort(string sortType) { _currentSortType = sortType; List<Song> sortedList = null; var sourceList = DisplayedTracks.ToList(); switch (sortType) { case "Title_AZ": sortedList = sourceList.OrderBy(s => s.Title).ToList(); break; case "Title_ZA": sortedList = sourceList.OrderByDescending(s => s.Title).ToList(); break; case "Artist": sortedList = sourceList.OrderBy(s => s.Artist).ToList(); break; case "Album": sortedList = sourceList.OrderBy(s => s.Album).ToList(); break; case "Duration_Short": sortedList = sourceList.OrderBy(s => s.Duration).ToList(); break; case "Duration_Long": sortedList = sourceList.OrderByDescending(s => s.Duration).ToList(); break; case "DateAdded": sortedList = sourceList.OrderByDescending(s => s.DateAdded).ToList(); break; } if (sortedList != null) DisplayedTracks = new ObservableCollection<Song>(sortedList); }
        public void ChangeVolume(double step) { Volume = Math.Clamp(Volume + step, 0, 100); }
        public void SeekTo(double value) { MusicPlayer.Seek(value); }
    }
}