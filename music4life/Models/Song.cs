using music4life.ViewModels;
using SQLite; // ✅ BẮT BUỘC PHẢI CÓ
using System;
using System.Windows.Media;

namespace music4life.Models
{
    public class Song : BaseViewModel
    {
        [PrimaryKey]
        public string FilePath { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public string Year { get; set; }
        public string Duration { get; set; }
        public DateTime DateAdded { get; set; }
        public string TechnicalInfo { get; set; }
        [Ignore]
        public ImageSource CoverImage { get; set; }

        private bool _isPlaying;
        [Ignore]
        public bool IsPlaying
        {
            get => _isPlaying;
            set { if (_isPlaying != value) { _isPlaying = value; OnPropertyChanged(); } }
        }

        private bool _isFavorite;
        [Ignore]
        public bool IsFavorite
        {
            get => _isFavorite;
            set { if (_isFavorite != value) { _isFavorite = value; OnPropertyChanged(); } }
        }
    }
}