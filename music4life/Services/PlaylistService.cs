using music4life.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace music4life.Services
{
    public static class PlaylistService
    {
        public static ObservableCollection<Playlist> AllPlaylists { get; private set; } = new ObservableCollection<Playlist>();

        static PlaylistService()
        {
            DatabaseService.Init();
            Load();
        }

        public static void Load()
        {
            var playlists = DatabaseService.Conn.Table<Playlist>().ToList();

            foreach (var pl in playlists)
            {
                var entries = DatabaseService.Conn.Table<PlaylistEntry>()
                                                .Where(x => x.PlaylistId == pl.Id)
                                                .ToList();
                pl.SongPaths = entries.Select(x => x.SongPath).ToList();
            }

            AllPlaylists = new ObservableCollection<Playlist>(playlists);
        }

        public static void CreatePlaylist(string name)
        {
            if (AllPlaylists.Any(p => p.Name == name)) return;

            var newPl = new Playlist { Name = name, CreatedDate = System.DateTime.Now };

            DatabaseService.Conn.Insert(newPl);

            AllPlaylists.Add(newPl);
        }

        public static void AddSongToPlaylist(Playlist playlist, string songPath)
        {
            if (playlist != null && !playlist.SongPaths.Contains(songPath))
            {
                playlist.SongPaths.Add(songPath);

                DatabaseService.Conn.Insert(new PlaylistEntry
                {
                    PlaylistId = playlist.Id,
                    SongPath = songPath
                });
            }
        }

        public static void RenamePlaylist(Playlist playlist, string newName)
        {
            if (playlist == null || string.IsNullOrWhiteSpace(newName)) return;

            if (AllPlaylists.Any(p => p.Id != playlist.Id && p.Name == newName)) return;

            playlist.Name = newName;

            DatabaseService.Conn.Update(playlist);
        }

        public static void DeletePlaylist(Playlist playlist)
        {
            if (playlist == null) return;

            if (AllPlaylists.Contains(playlist))
            {
                DatabaseService.Conn.Delete<Playlist>(playlist.Id);

                DatabaseService.Conn.Execute("DELETE FROM PlaylistEntry WHERE PlaylistId = ?", playlist.Id);

                AllPlaylists.Remove(playlist);
            }
        }
    }
}