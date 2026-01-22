using music4life.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace music4life.Services
{
    public static class MusicManager
    {
        public static ObservableCollection<Song> AllTracks { get; set; } = new ObservableCollection<Song>();

        public static async Task ScanMusic(List<string> folderPaths)
        {
            DatabaseService.Init();

            var cachedSongs = DatabaseService.Conn.Table<Song>().ToList();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks.Clear();
                foreach (var s in cachedSongs) AllTracks.Add(s);
            });

            await Task.Run(() =>
            {
                var newSongsBuffer = new List<Song>();
                var allFoundPaths = new HashSet<string>();

                DatabaseService.Conn.RunInTransaction(() =>
                {
                    foreach (var folder in folderPaths)
                    {
                        if (!Directory.Exists(folder)) continue;

                        var files = GetFilesSafe(folder);

                        foreach (var file in files)
                        {
                            allFoundPaths.Add(file);

                            var existing = DatabaseService.Conn.Find<Song>(file);
                            if (existing == null)
                            {
                                try
                                {
                                    Song song = null;
                                    try
                                    {
                                        using (var tfile = TagLib.File.Create(file))
                                        {
                                            song = CreateSongFromTag(file, tfile);
                                        }
                                    }
                                    catch
                                    {
                                        song = CreateSongFromFileInfo(file);
                                    }

                                    if (song != null)
                                    {
                                        DatabaseService.Conn.InsertOrReplace(song);
                                        newSongsBuffer.Add(song);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                });

                foreach (var cached in cachedSongs)
                {
                    if (!allFoundPaths.Contains(cached.FilePath))
                    {
                        DatabaseService.Conn.Delete<Song>(cached.FilePath);
                    }
                }

                if (newSongsBuffer.Count > 0 || cachedSongs.Count != allFoundPaths.Count)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var finalList = DatabaseService.Conn.Table<Song>().OrderBy(s => s.Title).ToList();
                        AllTracks.Clear();
                        foreach (var s in finalList) AllTracks.Add(s);
                    });
                }
            });
        }
        private static List<string> GetFilesSafe(string rootPath)
        {
            var result = new List<string>();
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                try
                {
                    var files = Directory.GetFiles(dir, "*.*")
                                         .Where(s => s.EndsWith(".mp3") || s.EndsWith(".flac") || s.EndsWith(".wav") || s.EndsWith(".m4a"));
                    result.AddRange(files);

                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        stack.Push(subDir);
                    }
                }
                catch { continue; }
            }
            return result;
        }

        private static Song CreateSongFromTag(string file, TagLib.File tfile)
        {
            var props = tfile.Properties;
            return new Song
            {
                FilePath = file,
                Title = !string.IsNullOrWhiteSpace(tfile.Tag.Title) ? tfile.Tag.Title : Path.GetFileNameWithoutExtension(file),
                Artist = !string.IsNullOrWhiteSpace(tfile.Tag.FirstPerformer) ? tfile.Tag.FirstPerformer : "Unknown Artist",
                Album = !string.IsNullOrWhiteSpace(tfile.Tag.Album) ? tfile.Tag.Album : "Unknown Album",
                Genre = tfile.Tag.FirstGenre ?? "Unknown",
                Year = tfile.Tag.Year > 0 ? tfile.Tag.Year.ToString() : "",
                Duration = props.Duration.ToString(@"mm\:ss"),
                DateAdded = File.GetCreationTime(file),
                TechnicalInfo = $"{props.AudioBitrate}kbps"
            };
        }
        private static Song CreateSongFromFileInfo(string file)
        {
            return new Song
            {
                FilePath = file,
                Title = Path.GetFileNameWithoutExtension(file),
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                Genre = "Unknown",
                Year = "",
                Duration = "00:00",
                DateAdded = File.GetCreationTime(file),
                TechnicalInfo = "Unknown format"
            };
        }

    }
}