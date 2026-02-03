using music4life.Models;
using SQLite;
using System;
using System.IO;

namespace music4life.Services
{
    public static class DatabaseService
    {
        public static SQLiteConnection Conn { get; private set; }

        public static string DbPath { get; private set; }

        public static void Init()
        {
            if (Conn != null) return;
            string roamingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "music4life");
            if (!Directory.Exists(roamingFolder))
            {
                Directory.CreateDirectory(roamingFolder);
            }

            DbPath = Path.Combine(roamingFolder, "music4life.db");

            Conn = new SQLiteConnection(DbPath);
            Conn.CreateTable<Song>();
            Conn.CreateTable<Playlist>();
            Conn.CreateTable<PlaylistEntry>();
            Conn.CreateTable<FavoriteEntry>();
        }
    }
}