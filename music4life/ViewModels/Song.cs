using System;
using System.Windows.Media;
using music4life.ViewModels;

namespace music4life.Models
{
    public class Song : BaseViewModel
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public string Year { get; set; }
        public string Duration { get; set; }

        public string FilePath { get; set; }

        public DateTime DateAdded { get; set; }

        public ImageSource CoverImage { get; set; }

        public string TechnicalInfo { get; set; }



        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}