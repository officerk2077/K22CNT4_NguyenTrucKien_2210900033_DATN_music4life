using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace music4life.Services
{
    public static class FavoriteService
    {
        private static string _filePath = "favorites.json";

        public static HashSet<string> FavoritePaths { get; private set; } = new HashSet<string>();

        static FavoriteService()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        FavoritePaths = new HashSet<string>(list);
                    }
                }
                catch { }
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(FavoritePaths);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }

        public static void Add(string path)
        {
            if (FavoritePaths.Add(path))
            {
                Save();
            }
        }

        public static void Remove(string path)
        {
            if (FavoritePaths.Remove(path))
            {
                Save();
            }
        }

        public static bool IsFavorite(string path)
        {
            return FavoritePaths.Contains(path);
        }
    }
}