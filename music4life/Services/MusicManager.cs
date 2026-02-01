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

            // Lấy danh sách từ cache (DB) trước
            var cachedSongs = DatabaseService.Conn.Table<Song>().ToList();
            var dbMap = cachedSongs.ToDictionary(s => s.FilePath, s => s);

            // [TỐI ƯU 1] Gán trực tiếp list mới thay vì Add từng phần tử gây lag
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks = new ObservableCollection<Song>(cachedSongs);
            });

            // Chạy quét file ở luồng nền (Background Thread)
            await Task.Run(() =>
            {
                var filesToProcess = new List<string>();
                var allFoundPaths = new HashSet<string>();

                foreach (var folder in folderPaths)
                {
                    if (Directory.Exists(folder))
                    {
                        var files = GetFilesSafe(folder);
                        foreach (var file in files)
                        {
                            allFoundPaths.Add(file);

                            bool needsUpdate = true;
                            if (dbMap.TryGetValue(file, out var existing))
                            {
                                // Nếu đã có thông tin kỹ thuật đầy đủ thì không cần quét lại Tag
                                if (!string.IsNullOrEmpty(existing.TechnicalInfo) && existing.TechnicalInfo.Contains("|"))
                                {
                                    needsUpdate = false;
                                }
                            }

                            if (needsUpdate)
                            {
                                filesToProcess.Add(file);
                            }
                        }
                    }
                }

                var newSongsBag = new System.Collections.Concurrent.ConcurrentBag<Song>();

                // Xử lý đọc Tag đa luồng
                Parallel.ForEach(filesToProcess, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (file) =>
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
                            newSongsBag.Add(song);
                        }
                    }
                    catch { }
                });

                // Cập nhật Database nếu có thay đổi
                if (!newSongsBag.IsEmpty || cachedSongs.Count != allFoundPaths.Count)
                {
                    DatabaseService.Conn.RunInTransaction(() =>
                    {
                        foreach (var song in newSongsBag)
                        {
                            DatabaseService.Conn.InsertOrReplace(song);
                        }

                        foreach (var cached in cachedSongs)
                        {
                            if (!allFoundPaths.Contains(cached.FilePath))
                            {
                                DatabaseService.Conn.Delete<Song>(cached.FilePath);
                            }
                        }
                    });

                    // [TỐI ƯU 2] Cập nhật lại UI một lần duy nhất sau khi quét xong và sắp xếp
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var finalList = DatabaseService.Conn.Table<Song>().OrderBy(s => s.Title).ToList();
                        AllTracks = new ObservableCollection<Song>(finalList);
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
                    // [TỐI ƯU 3] Thêm StringComparison.OrdinalIgnoreCase để không bỏ sót file .MP3, .WAV chữ hoa
                    var files = Directory.GetFiles(dir, "*.*")
                                         .Where(s => s.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                                     s.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                                                     s.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                                     s.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase));
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

            string ext = Path.GetExtension(file)?.TrimStart('.').ToUpper() ?? "UNK";

            double sampleRateKHz = props.AudioSampleRate / 1000.0;

            string channels = props.AudioChannels == 2 ? "Stereo" : (props.AudioChannels == 1 ? "Mono" : $"{props.AudioChannels} ch");

            int bits = props.BitsPerSample;

            string techInfo;
            if (bits > 0 && bits != 32)
            {
                techInfo = $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {bits}-bit | {channels}";
            }
            else
            {
                techInfo = $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {channels}";
            }

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
                TechnicalInfo = techInfo
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