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

            DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "music4life.db");

            Conn = new SQLiteConnection(DbPath);

            Conn.CreateTable<Song>();
            Conn.CreateTable<Playlist>();
            Conn.CreateTable<PlaylistEntry>();
            Conn.CreateTable<FavoriteEntry>();
        }
    }
}