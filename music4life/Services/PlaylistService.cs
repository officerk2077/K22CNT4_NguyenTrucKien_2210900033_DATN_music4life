using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using music4life.Models;

namespace music4life.Services
{
    public static class PlaylistService
    {
        private static string _filePath = "playlists.json";
        public static ObservableCollection<Playlist> AllPlaylists { get; private set; } = new ObservableCollection<Playlist>();

        static PlaylistService()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<List<Playlist>>(json);
                    if (list != null)
                    {
                        AllPlaylists = new ObservableCollection<Playlist>(list);
                    }
                }
                catch { }
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(AllPlaylists);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public static void CreatePlaylist(string name)
        {
            if (AllPlaylists.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return;

            var newPl = new Playlist { Name = name };
            AllPlaylists.Add(newPl);
            Save();
        }

        public static void AddSongToPlaylist(Playlist playlist, string songPath)
        {
            if (playlist != null && !playlist.SongPaths.Contains(songPath))
            {
                playlist.SongPaths.Add(songPath);
                Save();
            }
        }

        public static void DeletePlaylist(Playlist playlist)
        {
            if (playlist == null) return;

            var existing = AllPlaylists.FirstOrDefault(p => p.Name == playlist.Name);
            if (existing != null)
            {
                AllPlaylists.Remove(existing);
            }

            Save();
        }
    }
}   