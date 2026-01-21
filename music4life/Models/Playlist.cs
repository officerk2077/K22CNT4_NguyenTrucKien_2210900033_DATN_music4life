using SQLite; // ✅ Bắt buộc có để dùng [PrimaryKey]
using music4life.ViewModels;
using System;
using System.Collections.Generic;

namespace music4life.Models
{
    public class Playlist : BaseViewModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Ignore]
        public List<string> SongPaths { get; set; } = new List<string>();

        [Ignore]
        public string SongCountText => $"{SongPaths?.Count ?? 0} bài hát";
    }
}