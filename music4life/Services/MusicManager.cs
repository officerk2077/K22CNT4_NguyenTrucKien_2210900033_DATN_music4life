using music4life.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Application = System.Windows.Application;

namespace music4life.Services
{
    public static class MusicManager
    {
        private static readonly object _listLock = new object();
        public static ObservableCollection<Song> AllTracks { get; set; }

        private static readonly object _dbLock = new object();

        public static event Action<bool> IsScanningChanged;

        static MusicManager()
        {
            AllTracks = new ObservableCollection<Song>();
            BindingOperations.EnableCollectionSynchronization(AllTracks, _listLock);
        }

        public static async Task ScanMusic(List<string> folderPaths)
        {
            IsScanningChanged?.Invoke(true);

            try
            {
                DatabaseService.Init();

                lock (_dbLock)
                {
                    try
                    {
                        DatabaseService.Conn.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");
                        DatabaseService.Conn.Execute("PRAGMA synchronous = OFF;");
                    }
                    catch { }
                }

                List<Song> cachedSongs;
                lock (_dbLock) cachedSongs = DatabaseService.Conn.Table<Song>().ToList();

                var songsToRemove = new List<Song>();
                var validSongs = new List<Song>();

                foreach (var song in cachedSongs)
                {
                    bool isValid = false;
                    foreach (var folder in folderPaths)
                    {
                        if (song.FilePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                        {
                            isValid = true;
                            break;
                        }
                    }
                    if (isValid) validSongs.Add(song);
                    else songsToRemove.Add(song);
                }

                if (songsToRemove.Count > 0)
                {
                    lock (_dbLock)
                    {
                        DatabaseService.Conn.RunInTransaction(() =>
                        {
                            foreach (var s in songsToRemove) DatabaseService.Conn.Delete<Song>(s.FilePath);
                        });
                    }
                }

                var dbMap = new Dictionary<string, Song>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in validSongs) dbMap[s.FilePath] = s;

                lock (_listLock)
                {
                    AllTracks.Clear();
                    foreach (var s in validSongs) AllTracks.Add(s);
                }

                await Task.Run(() =>
                {
                    var bufferFiles = new List<string>();
                    var allFoundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var file in EnumerateAllFiles(folderPaths))
                    {
                        allFoundPaths.Add(file);

                        if (dbMap.ContainsKey(file)) continue;

                        bufferFiles.Add(file);

                        if (bufferFiles.Count >= 20)
                        {
                            ProcessAndDisplayBatch(bufferFiles);
                            bufferFiles.Clear();
                        }
                    }

                    if (bufferFiles.Count > 0) ProcessAndDisplayBatch(bufferFiles);

                    var missingFiles = validSongs.Where(s => !allFoundPaths.Contains(s.FilePath)).ToList();
                    if (missingFiles.Count > 0)
                    {
                        lock (_dbLock)
                        {
                            DatabaseService.Conn.RunInTransaction(() =>
                            {
                                foreach (var s in missingFiles) DatabaseService.Conn.Delete<Song>(s.FilePath);
                            });
                        }
                        lock (_listLock)
                        {
                            foreach (var s in missingFiles)
                            {
                                var item = AllTracks.FirstOrDefault(x => x.FilePath == s.FilePath);
                                if (item != null) AllTracks.Remove(item);
                            }
                        }
                    }
                });
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsScanningChanged?.Invoke(false);
                });
            }
        }

        private static void ProcessAndDisplayBatch(List<string> files)
        {
            var processedSongs = new ConcurrentBag<Song>();

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (file) =>
            {
                try
                {
                    using (var tfile = TagLib.File.Create(file, TagLib.ReadStyle.Average))
                    {
                        processedSongs.Add(CreateSongFromTag(file, tfile));
                    }
                }
                catch
                {
                    processedSongs.Add(CreateSongFromFileInfo(file));
                }
            });

            if (!processedSongs.IsEmpty)
            {
                lock (_dbLock)
                {
                    DatabaseService.Conn.RunInTransaction(() =>
                    {
                        foreach (var s in processedSongs) DatabaseService.Conn.InsertOrReplace(s);
                    });
                }

                lock (_listLock)
                {
                    foreach (var s in processedSongs)
                    {
                        if (!AllTracks.Any(x => x.FilePath == s.FilePath)) AllTracks.Add(s);
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateAllFiles(List<string> folders)
        {
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                var stack = new Stack<string>();
                stack.Push(folder);

                while (stack.Count > 0)
                {
                    var dir = stack.Pop();

                    List<string> filesInCurrentDir = null;

                    try
                    {
                        filesInCurrentDir = Directory.EnumerateFiles(dir, "*.*").ToList();

                        foreach (var subDir in Directory.EnumerateDirectories(dir))
                        {
                            stack.Push(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }
                    catch { continue; }

                    if (filesInCurrentDir != null)
                    {
                        foreach (var file in filesInCurrentDir)
                        {
                            if (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                file.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                                file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                file.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                            {
                                yield return file;
                            }
                        }
                    }
                }
            }
        }

        private static Song CreateSongFromTag(string file, TagLib.File tfile)
        {
            var props = tfile.Properties;
            var tag = tfile.Tag;
            string ext = Path.GetExtension(file)?.TrimStart('.').ToUpper() ?? "UNK";

            string artistFull = "Unknown Artist";
            if (tag.Performers != null && tag.Performers.Length > 0)
            {
                artistFull = string.Join(", ", tag.Performers);
            }
            else if (!string.IsNullOrWhiteSpace(tag.FirstPerformer))
            {
                artistFull = tag.FirstPerformer;
            }

            string channels = props.AudioChannels == 1 ? "Mono" : "Stereo";
            if (props.AudioChannels > 2) channels = $"{props.AudioChannels} Ch";

            string bitDepth = props.BitsPerSample > 0 ? $" | {props.BitsPerSample} bit" : "";

            string techInfoFull = $"{ext}" +
                                  $" | {props.AudioBitrate} kbps" +
                                  $" | {props.AudioSampleRate / 1000.0:F1} kHz" +
                                  bitDepth +
                                  $" | {channels}";

            return new Song
            {
                FilePath = file,
                Title = !string.IsNullOrWhiteSpace(tag.Title) ? tag.Title : Path.GetFileNameWithoutExtension(file),
                Artist = artistFull,
                Album = !string.IsNullOrWhiteSpace(tag.Album) ? tag.Album : "Unknown Album",
                Genre = tag.FirstGenre ?? "Unknown",
                Year = tag.Year > 0 ? tag.Year.ToString() : "",
                Duration = props.Duration.ToString(@"mm\:ss"),
                DateAdded = File.GetCreationTime(file),
                TechnicalInfo = techInfoFull,

                IsMetadataLoaded = true
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
                TechnicalInfo = "Unknown format",
                IsMetadataLoaded = true
            };
        }
    }
}