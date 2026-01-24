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

            // Load danh sách cũ từ DB lên UI trước để người dùng không phải chờ
            var cachedSongs = DatabaseService.Conn.Table<Song>().ToList();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks.Clear();
                foreach (var s in cachedSongs) AllTracks.Add(s);
            });

            // Chạy task quét nhạc ngầm
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

                            // --- LOGIC MỚI: Kiểm tra xem có cần quét lại không ---
                            // 1. Nếu bài hát chưa có trong DB (existing == null) -> Quét mới
                            // 2. Nếu bài hát đã có nhưng thông tin kỹ thuật chưa chứa dấu "|" (tức là format cũ) -> Quét lại
                            bool needsUpdate = existing == null || string.IsNullOrEmpty(existing.TechnicalInfo) || !existing.TechnicalInfo.Contains("|");

                            if (needsUpdate)
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
                                        // InsertOrReplace: Nếu chưa có thì thêm, có rồi thì cập nhật
                                        DatabaseService.Conn.InsertOrReplace(song);
                                        newSongsBuffer.Add(song);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                });

                // Xóa các bài hát không còn tồn tại trong ổ cứng
                foreach (var cached in cachedSongs)
                {
                    if (!allFoundPaths.Contains(cached.FilePath))
                    {
                        DatabaseService.Conn.Delete<Song>(cached.FilePath);
                    }
                }

                // Nếu có sự thay đổi (có bài mới, bài cập nhật, hoặc bài bị xóa) thì refresh lại UI
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

            // --- CẬP NHẬT LOGIC HIỂN THỊ THÔNG TIN KỸ THUẬT ---

            // 1. Lấy đuôi file (MP3, FLAC...)
            string ext = Path.GetExtension(file)?.TrimStart('.').ToUpper() ?? "UNK";

            // 2. Tần số lấy mẫu (Sample Rate) -> Đổi sang kHz
            double sampleRateKHz = props.AudioSampleRate / 1000.0;

            // 3. Kênh âm thanh (Stereo/Mono)
            string channels = props.AudioChannels == 2 ? "Stereo" : (props.AudioChannels == 1 ? "Mono" : $"{props.AudioChannels} ch");

            // 4. Độ sâu Bit (Bit Depth)
            int bits = props.BitsPerSample;

            string techInfo;
            // Nếu là file chất lượng cao (thường > 0 và khác 32-float mặc định của MP3)
            if (bits > 0 && bits != 32)
            {
                // Ví dụ: FLAC | 2692 kbps | 96 kHz | 24-bit | Stereo
                techInfo = $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {bits}-bit | {channels}";
            }
            else
            {
                // Ví dụ: MP3 | 320 kbps | 44.1 kHz | Stereo
                techInfo = $"{ext} | {props.AudioBitrate} kbps | {sampleRateKHz} kHz | {channels}";
            }

            // --- KẾT THÚC CẬP NHẬT ---

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