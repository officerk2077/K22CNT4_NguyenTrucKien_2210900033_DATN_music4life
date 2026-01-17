using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;
using music4life.Models;

namespace music4life.Services
{
    public enum RepeatMode { None, RepeatOne, RepeatAll }

    public static class MusicPlayer
    {
        private static MediaPlayer _player1 = new MediaPlayer();
        private static MediaPlayer _player2 = new MediaPlayer();
        private static MediaPlayer _activePlayer;

        private static DispatcherTimer _timer;
        private static DispatcherTimer _crossfadeTimer;

        public static double CrossfadeDuration { get; set; } = 3.0;
        private static double _masterVolume = 0.5;

        private static bool _isTransitioning = false;

        private static Random _rng = new Random();
        public static List<Song> CurrentPlaylist { get; set; } = new List<Song>();
        private static List<Song> _originalPlaylist;

        public static Song CurrentTrack { get; private set; }
        public static bool IsShuffle { get; private set; } = false;
        public static RepeatMode CurrentRepeatMode { get; set; } = RepeatMode.None;
        public static bool IsPlaying { get; private set; } = false;

        public static event Action SongChanged;
        public static event Action<bool> PlaybackStateChanged;
        public static event Action<TimeSpan, TimeSpan> PositionChanged;

        static MusicPlayer()
        {
            _activePlayer = _player1;
            _player1.Volume = _masterVolume;
            _player2.Volume = 0;

            _player1.MediaEnded += (s, e) => HandleMediaEnded(_player1);
            _player2.MediaEnded += (s, e) => HandleMediaEnded(_player2);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            _crossfadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _crossfadeTimer.Tick += Crossfade_Tick;
        }

        private static void Crossfade_Tick(object sender, EventArgs e)
        {
            var fadingPlayer = (_activePlayer == _player1) ? _player2 : _player1;
            double step = _masterVolume / (CrossfadeDuration * 10);

            if (_activePlayer.Volume < _masterVolume)
                _activePlayer.Volume = Math.Min(_masterVolume, _activePlayer.Volume + step);

            if (fadingPlayer.Volume > 0)
            {
                fadingPlayer.Volume = Math.Max(0, fadingPlayer.Volume - step);
            }
            else
            {
                fadingPlayer.Stop();
                if (_activePlayer.Volume >= _masterVolume) _crossfadeTimer.Stop();
            }
        }

        public static void PlayTrack(Song song, List<Song> playlist = null, bool isAuto = false)
        {
            if (song == null) return;

            if (playlist != null && playlist.Count > 0)
            {
                if (IsShuffle)
                {
                    _originalPlaylist = new List<Song>(playlist);
                    CurrentPlaylist = ShuffleList(new List<Song>(playlist), song);
                }
                else
                {
                    CurrentPlaylist = new List<Song>(playlist);
                    _originalPlaylist = null;
                }
            }

            CurrentTrack = song;
            _isTransitioning = false;

            try
            {
                var nextPlayer = (_activePlayer == _player1) ? _player2 : _player1;
                var oldPlayer = _activePlayer;

                nextPlayer.Open(new Uri(song.FilePath));

                if (CrossfadeDuration > 0 && isAuto)
                {
                    nextPlayer.Volume = 0;
                    _activePlayer = nextPlayer;
                    nextPlayer.Play();
                    _crossfadeTimer.Start();
                }
                else
                {
                    oldPlayer.Stop();
                    oldPlayer.Volume = 0;
                    _crossfadeTimer.Stop();

                    nextPlayer.Volume = _masterVolume; 
                    _activePlayer = nextPlayer;
                    nextPlayer.Play();
                }

                IsPlaying = true;
                _timer.Start();

                SongChanged?.Invoke();
                PlaybackStateChanged?.Invoke(true);
            }
            catch { Next(); }
        }

        public static void Play() { _activePlayer.Play(); IsPlaying = true; _timer.Start(); PlaybackStateChanged?.Invoke(true); }
        public static void Pause() { _player1.Pause(); _player2.Pause(); _crossfadeTimer.Stop(); IsPlaying = false; _timer.Stop(); PlaybackStateChanged?.Invoke(false); }
        public static void TogglePlayPause() { if (IsPlaying) Pause(); else Play(); }

        public static void Seek(double seconds)
        {
            _activePlayer.Position = TimeSpan.FromSeconds(seconds);
            if (_activePlayer.NaturalDuration.HasTimeSpan)
            {
                var remaining = _activePlayer.NaturalDuration.TimeSpan.TotalSeconds - seconds;
                if (remaining > CrossfadeDuration + 2) _isTransitioning = false;
            }
            if (_activePlayer.NaturalDuration.HasTimeSpan) PositionChanged?.Invoke(_activePlayer.Position, _activePlayer.NaturalDuration.TimeSpan);
        }

        public static void SetVolume(double volume)
        {
            _masterVolume = Math.Clamp(volume / 100, 0, 1);
            if (!_crossfadeTimer.IsEnabled)
            {
                _player1.Volume = (_activePlayer == _player1) ? _masterVolume : 0;
                _player2.Volume = (_activePlayer == _player2) ? _masterVolume : 0;
            }
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            if (IsPlaying && _activePlayer.Source != null && _activePlayer.NaturalDuration.HasTimeSpan)
            {
                var total = _activePlayer.NaturalDuration.TimeSpan;
                var current = _activePlayer.Position;
                var remaining = total - current;

                PositionChanged?.Invoke(current, total);

                if (CrossfadeDuration > 0 &&
                    total.TotalSeconds > CrossfadeDuration &&
                    remaining.TotalSeconds <= CrossfadeDuration &&
                    !_isTransitioning)
                {
                    if (CanPlayNext())
                    {
                        _isTransitioning = true;
                        Next(isAuto: true);
                    }
                }
            }
        }

        private static bool CanPlayNext()
        {
            if (CurrentPlaylist == null || CurrentPlaylist.Count == 0) return false;
            if (CurrentRepeatMode == RepeatMode.RepeatOne) return true;
            if (CurrentRepeatMode == RepeatMode.RepeatAll) return true;
            int index = CurrentPlaylist.IndexOf(CurrentTrack);
            return (index < CurrentPlaylist.Count - 1);
        }

        private static void HandleMediaEnded(MediaPlayer sender)
        {
            if (sender != _activePlayer) return;

            if (CurrentRepeatMode == RepeatMode.RepeatOne)
            {
                sender.Position = TimeSpan.Zero;
                sender.Play();
            }
            else
            {
                if (!_isTransitioning) Next(isAuto: true);
            }
        }

        public static void Next(bool isAuto = false)
        {
            if (CurrentPlaylist == null || CurrentPlaylist.Count == 0) return;
            int index = CurrentPlaylist.IndexOf(CurrentTrack);
            if (index < CurrentPlaylist.Count - 1) PlayTrack(CurrentPlaylist[index + 1], null, isAuto);
            else if (CurrentRepeatMode == RepeatMode.RepeatAll) PlayTrack(CurrentPlaylist[0], null, isAuto);
            else
            {
                Pause();
                _activePlayer.Position = TimeSpan.Zero;
                _isTransitioning = false;
            }
        }

        public static void Previous()
        {
            if (CurrentPlaylist == null || CurrentPlaylist.Count == 0) return;
            if (_activePlayer.Position.TotalSeconds > 5) { Seek(0); return; }
            int index = CurrentPlaylist.IndexOf(CurrentTrack);
            if (index > 0) PlayTrack(CurrentPlaylist[index - 1], null, isAuto: false);
            else PlayTrack(CurrentPlaylist[CurrentPlaylist.Count - 1], null, isAuto: false);
        }

        public static void ToggleShuffle()
        {
            IsShuffle = !IsShuffle;
            if (CurrentPlaylist == null || CurrentPlaylist.Count == 0) return;
            if (IsShuffle)
            {
                _originalPlaylist = new List<Song>(CurrentPlaylist);
                CurrentPlaylist = ShuffleList(new List<Song>(CurrentPlaylist), CurrentTrack);
            }
            else
            {
                if (_originalPlaylist != null) { CurrentPlaylist = new List<Song>(_originalPlaylist); _originalPlaylist = null; }
            }
        }

        private static List<Song> ShuffleList(List<Song> inputList, Song currentSong)
        {
            var list = new List<Song>(inputList);
            int n = list.Count;
            while (n > 1) { n--; int k = _rng.Next(n + 1); (list[k], list[n]) = (list[n], list[k]); }
            if (currentSong != null && list.Contains(currentSong)) { list.Remove(currentSong); list.Insert(0, currentSong); }
            return list;
        }
    }
}