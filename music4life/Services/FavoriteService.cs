using music4life.Models;
using System.Collections.Generic;
using System.Linq;

namespace music4life.Services
{
    public static class FavoriteService
    {
        public static HashSet<string> FavoritePaths { get; private set; } = new HashSet<string>();

        static FavoriteService()
        {
            DatabaseService.Init();
            Load();
        }

        public static void Load()
        {
            var list = DatabaseService.Conn.Table<FavoriteEntry>().ToList();
            FavoritePaths = new HashSet<string>(list.Select(x => x.SongPath));
        }

        public static void Add(string path)
        {
            if (FavoritePaths.Add(path))
            {
                DatabaseService.Conn.InsertOrReplace(new FavoriteEntry { SongPath = path });
            }
        }

        public static void Remove(string path)
        {
            if (FavoritePaths.Remove(path))
            {
                DatabaseService.Conn.Delete<FavoriteEntry>(path);
            }
        }

        public static bool IsFavorite(string path) => FavoritePaths.Contains(path);
    }
}