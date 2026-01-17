using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using music4life.Models;

namespace music4life.Services
{
    public static class MusicManager
    {
        public static ObservableCollection<Song> AllTracks { get; set; } = new ObservableCollection<Song>();

        public static async Task ScanMusic(List<string> folderPaths)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => AllTracks.Clear());

            var tempSongs = new List<Song>();

            await Task.Run(() =>
            {
                foreach (var folder in folderPaths)
                {
                    if (!Directory.Exists(folder)) continue;

                    try
                    {
                        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                                             .Where(s => s.EndsWith(".mp3") || s.EndsWith(".flac") || s.EndsWith(".wav") || s.EndsWith(".m4a"));

                        foreach (var file in files)
                        {
                            try
                            {
                                using (var tfile = TagLib.File.Create(file))
                                {
                                    var fileInfo = new FileInfo(file);
                                    var props = tfile.Properties;

                                    string ext = Path.GetExtension(file).TrimStart('.').ToUpper();

                                    double sampleRate = props.AudioSampleRate / 1000.0;

                                    string channels = props.AudioChannels == 2 ? "Stereo" : (props.AudioChannels == 1 ? "Mono" : $"{props.AudioChannels}ch");

                                    string techInfo = $"{ext} {sampleRate:0.##} kHz, {props.AudioBitrate}k, {channels}";

                                    var song = new Song
                                    {
                                        Title = string.IsNullOrWhiteSpace(tfile.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tfile.Tag.Title,
                                        Artist = string.IsNullOrWhiteSpace(tfile.Tag.FirstPerformer) ? "Unknown Artist" : tfile.Tag.FirstPerformer,
                                        Album = string.IsNullOrWhiteSpace(tfile.Tag.Album) ? "Unknown Album" : tfile.Tag.Album,
                                        Genre = tfile.Tag.FirstGenre ?? "Unknown",
                                        Year = tfile.Tag.Year == 0 ? "Unknown" : tfile.Tag.Year.ToString(),
                                        FilePath = file,
                                        Duration = tfile.Properties.Duration.ToString(@"mm\:ss"),

                                        DateAdded = fileInfo.CreationTime,

                                        TechnicalInfo = techInfo,

                                        CoverImage = null
                                    };

                                    tempSongs.Add(song);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks = new ObservableCollection<Song>(tempSongs);
            });
        }
    }
}