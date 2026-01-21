using music4life.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace music4life.Services
{
    public static class MusicManager
    {
        public static ObservableCollection<Song> AllTracks { get; set; } = new ObservableCollection<Song>();

        public static async Task ScanMusic(List<string> folderPaths)
        {
            DatabaseService.Init();

            // 1. LOAD CACHE TỪ DB (Hiển thị ngay lập tức)
            var cachedSongs = DatabaseService.Conn.Table<Song>().ToList();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AllTracks.Clear();
                foreach (var s in cachedSongs) AllTracks.Add(s);
            });

            // 2. QUÉT THỰC TẾ (Chạy ngầm)
            await Task.Run(() =>
            {
                var newSongsBuffer = new List<Song>();
                var allFoundPaths = new HashSet<string>();

                // Dùng Transaction để tăng tốc độ ghi vào DB gấp 10 lần
                DatabaseService.Conn.RunInTransaction(() =>
                {
                    foreach (var folder in folderPaths)
                    {
                        if (!Directory.Exists(folder)) continue;

                        // ✅ SỬ DỤNG HÀM QUÉT AN TOÀN (Thay vì EnumerateFiles mặc định)
                        var files = GetFilesSafe(folder);

                        foreach (var file in files)
                        {
                            allFoundPaths.Add(file);

                            // Nếu bài hát chưa có trong DB thì mới đọc thẻ Tag
                            var existing = DatabaseService.Conn.Find<Song>(file);
                            if (existing == null)
                            {
                                try
                                {
                                    // Fallback: Nếu TagLib lỗi thì vẫn tạo bài hát bằng tên file
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
                                        // File lỗi tag hoặc không đọc được -> Tạo info từ tên file
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

                // 3. Dọn dẹp DB: Xóa những bài không còn trên ổ cứng
                foreach (var cached in cachedSongs)
                {
                    if (!allFoundPaths.Contains(cached.FilePath))
                    {
                        DatabaseService.Conn.Delete<Song>(cached.FilePath);
                    }
                }

                // 4. Cập nhật UI lần cuối nếu có bài mới
                if (newSongsBuffer.Count > 0 || cachedSongs.Count != allFoundPaths.Count)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Reload lại toàn bộ từ DB để đảm bảo sắp xếp đúng
                        var finalList = DatabaseService.Conn.Table<Song>().OrderBy(s => s.Title).ToList();
                        AllTracks.Clear();
                        foreach (var s in finalList) AllTracks.Add(s);
                    });
                }

                // 5. Load ảnh bìa sau cùng
                LoadImagesAsync(AllTracks.ToList());
            });
        }

        // 🔥 HÀM QUAN TRỌNG: Quét đệ quy bỏ qua lỗi
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
                    // 1. Lấy các file trong thư mục hiện tại
                    var files = Directory.GetFiles(dir, "*.*")
                                         .Where(s => s.EndsWith(".mp3") || s.EndsWith(".flac") || s.EndsWith(".wav") || s.EndsWith(".m4a"));
                    result.AddRange(files);

                    // 2. Lấy các thư mục con và đẩy vào Stack để quét tiếp
                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        stack.Push(subDir);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // ⛔ Gặp folder bị cấm (System Volume, Trash...) -> BỎ QUA và đi tiếp
                    continue;
                }
                catch (Exception)
                {
                    continue;
                }
            }
            return result;
        }

        // Hàm tạo Song từ Tag (Chuẩn)
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
                TechnicalInfo = $"{props.AudioBitrate}kbps",
                CoverImage = null
            };
        }

        // Hàm tạo Song dự phòng (Khi TagLib lỗi)
        private static Song CreateSongFromFileInfo(string file)
        {
            return new Song
            {
                FilePath = file,
                Title = Path.GetFileNameWithoutExtension(file), // Lấy tên file làm tên bài
                Artist = "Unknown Artist",
                Album = "Unknown Album",
                Genre = "Unknown",
                Year = "",
                Duration = "00:00",
                DateAdded = File.GetCreationTime(file),
                TechnicalInfo = "Unknown format",
                CoverImage = null
            };
        }

        // --- Hàm Load ảnh (Giữ nguyên như cũ) ---
        private static void LoadImagesAsync(List<Song> songs)
        {
            foreach (var song in songs)
            {
                if (song.CoverImage == null)
                {
                    try
                    {
                        using (var tfile = TagLib.File.Create(song.FilePath))
                        {
                            var pic = tfile.Tag.Pictures.FirstOrDefault();
                            if (pic != null)
                            {
                                var bin = pic.Data.Data;
                                var bitmap = LoadImageFromBytes(bin);
                                System.Windows.Application.Current.Dispatcher.Invoke(() => song.CoverImage = bitmap);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private static BitmapImage LoadImageFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(bytes))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
    }
}