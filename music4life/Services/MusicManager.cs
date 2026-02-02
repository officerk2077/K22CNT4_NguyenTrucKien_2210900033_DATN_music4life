using music4life.Models;
using System;
using System.Collections.Concurrent;
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

        private static readonly object _dbLock = new object();

        public static async Task ScanMusic(List<string> folderPaths)
        {
            DatabaseService.Init();

            // 1. Load Cache
            List<Song> cachedSongs;
            lock (_dbLock)
            {
                cachedSongs = DatabaseService.Conn.Table<Song>().ToList();
            }

            var dbMap = new Dictionary<string, Song>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in cachedSongs)
            {
                if (!dbMap.ContainsKey(s.FilePath)) dbMap[s.FilePath] = s;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks = new ObservableCollection<Song>(cachedSongs);
            });

            await Task.Run(() =>
            {
                var filesToProcess = new List<string>();
                var allFoundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var folder in folderPaths)
                {
                    if (Directory.Exists(folder))
                    {
                        var files = GetFilesFast(folder);
                        foreach (var file in files)
                        {
                            allFoundPaths.Add(file);

                            // Chỉ quét file chưa có trong DB
                            if (!dbMap.ContainsKey(file))
                            {
                                filesToProcess.Add(file);
                            }
                        }
                    }
                }

                // Xóa file rác (đã xóa khỏi ổ cứng) khỏi DB
                if (cachedSongs.Count != allFoundPaths.Count)
                {
                    var pathsToDelete = cachedSongs.Where(s => !allFoundPaths.Contains(s.FilePath)).Select(s => s.FilePath).ToList();
                    if (pathsToDelete.Count > 0)
                    {
                        lock (_dbLock)
                        {
                            DatabaseService.Conn.RunInTransaction(() =>
                            {
                                foreach (var p in pathsToDelete) DatabaseService.Conn.Delete<Song>(p);
                            });
                        }

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            foreach (var p in pathsToDelete)
                            {
                                var item = AllTracks.FirstOrDefault(x => x.FilePath == p);
                                if (item != null) AllTracks.Remove(item);
                            }
                        });
                    }
                }

                // 2. XỬ LÝ QUÉT MỚI (TỐI ƯU HÓA)
                int batchSize = 20; // Tăng lên 20 để giảm số lần refresh UI
                var batches = filesToProcess
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / batchSize)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                // Tự động dùng tối đa số luồng CPU cho phép (nhanh hơn trên máy mạnh)
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

                foreach (var batch in batches)
                {
                    var newSongsBag = new ConcurrentBag<Song>();

                    Parallel.ForEach(batch, parallelOptions, (file) =>
                    {
                        try
                        {
                            Song song = null;
                            try
                            {
                                // [QUAN TRỌNG] ReadStyle.Average: Chỉ đọc thông tin cần thiết, bỏ qua check lỗi sâu
                                // Giúp đọc file mới nhanh hơn đáng kể
                                using (var tfile = TagLib.File.Create(file, TagLib.ReadStyle.Average))
                                {
                                    song = CreateSongFromTag(file, tfile);
                                }
                            }
                            catch
                            {
                                song = CreateSongFromFileInfo(file);
                            }

                            if (song != null) newSongsBag.Add(song);
                        }
                        catch { }
                    });

                    if (!newSongsBag.IsEmpty)
                    {
                        // Update UI
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            foreach (var s in newSongsBag)
                            {
                                if (!AllTracks.Any(x => x.FilePath == s.FilePath)) AllTracks.Add(s);
                            }
                        }));

                        // Save DB
                        lock (_dbLock)
                        {
                            DatabaseService.Conn.RunInTransaction(() =>
                            {
                                foreach (var s in newSongsBag) DatabaseService.Conn.InsertOrReplace(s);
                            });
                        }
                    }
                }
            });
        }

        private static List<string> GetFilesFast(string rootPath)
        {
            var result = new List<string>();
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.*"))
                    {
                        if (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(file);
                        }
                    }

                    foreach (var subDir in Directory.EnumerateDirectories(dir))
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

            // Xử lý null an toàn
            string title = !string.IsNullOrWhiteSpace(tfile.Tag.Title) ? tfile.Tag.Title : Path.GetFileNameWithoutExtension(file);
            string artist = !string.IsNullOrWhiteSpace(tfile.Tag.FirstPerformer) ? tfile.Tag.FirstPerformer : "Unknown Artist";
            string album = !string.IsNullOrWhiteSpace(tfile.Tag.Album) ? tfile.Tag.Album : "Unknown Album";
            string genre = !string.IsNullOrWhiteSpace(tfile.Tag.FirstGenre) ? tfile.Tag.FirstGenre : "Unknown";
            string year = tfile.Tag.Year > 0 ? tfile.Tag.Year.ToString() : "";

            double sampleRateKHz = props.AudioSampleRate / 1000.0;
            string channels = props.AudioChannels == 2 ? "Stereo" : (props.AudioChannels == 1 ? "Mono" : $"{props.AudioChannels} ch");
            int bits = props.BitsPerSample;

            string techInfo = (bits > 0 && bits != 32)
                ? $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {bits}-bit | {channels}"
                : $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {channels}";

            return new Song
            {
                FilePath = file,
                Title = title,
                Artist = artist,
                Album = album,
                Genre = genre,
                Year = year,
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