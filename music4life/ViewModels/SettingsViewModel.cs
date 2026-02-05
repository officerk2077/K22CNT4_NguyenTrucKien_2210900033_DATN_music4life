using music4life.Models;
using music4life.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace music4life.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private double _crossfadeSeconds;
        private bool _isMinimizeToTrayEnabled;
        private bool _isScanning;
        private string _statusMessage;
        private readonly string _settingsFilePath;

        public ObservableCollection<string> MusicFolders { get; set; }

        public double CrossfadeSeconds
        {
            get => _crossfadeSeconds;
            set
            {
                _crossfadeSeconds = value;
                OnPropertyChanged();
                music4life.Services.MusicPlayer.CrossfadeDuration = value;
            }
        }

        public bool IsMinimizeToTrayEnabled
        {
            get => _isMinimizeToTrayEnabled;
            set { _isMinimizeToTrayEnabled = value; OnPropertyChanged(); }
        }

        public bool IsNotScanning
        {
            get => !_isScanning;
            set { _isScanning = !value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand AddFolderCommand { get; set; }
        public ICommand RemoveFolderCommand { get; set; }
        public ICommand SaveSettingsCommand { get; set; }
        public ICommand RescanCommand { get; set; }

        public SettingsViewModel()
        {
            string roamingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "music4life");
            if (!Directory.Exists(roamingFolder)) Directory.CreateDirectory(roamingFolder);
            _settingsFilePath = Path.Combine(roamingFolder, "settings.json");

            MusicFolders = new ObservableCollection<string>();
            _isScanning = false;
            StatusMessage = "Sẵn sàng";

            LoadSettings();

            AddFolderCommand = new RelayCommand<object>((p) => AddFolder());
            RemoveFolderCommand = new RelayCommand<string>((path) => RemoveFolder(path));
            SaveSettingsCommand = new RelayCommand<object>((p) => SaveSettings(p));
            RescanCommand = new RelayCommand<object>(async (p) => await RescanLibrary());
        }

        private void ShowToast(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.ShowToast(message);
                }
            });
        }

        private void AddFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Chọn thư mục nhạc";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (!MusicFolders.Contains(dialog.SelectedPath))
                    {
                        MusicFolders.Add(dialog.SelectedPath);
                        StatusMessage = "Đã thêm thư mục. Hãy bấm 'Lưu' hoặc 'Quét lại'.";

                        ShowToast("Đã thêm thư mục! Bấm 'Lưu Cài Đặt' để cập nhật.");
                    }
                    else
                    {
                        ShowToast("Thư mục này đã có trong danh sách rồi!");
                    }
                }
            }
        }

        private void RemoveFolder(string path)
        {
            if (MusicFolders.Contains(path))
            {
                MusicFolders.Remove(path);
                StatusMessage = "Đã xóa thư mục. Nhớ bấm Lưu để áp dụng.";
            }
        }

        private async Task RescanLibrary()
        {
            if (_isScanning) return;

            if (MusicFolders.Count == 0)
            {
                if (Application.Current.MainWindow.DataContext is MainViewModel mainVm)
                {
                    mainVm.RefreshData(new List<Song>());
                }
                music4life.Services.MusicPlayer.CurrentPlaylist.Clear();
                MusicManager.AllTracks.Clear();
                StatusMessage = "Thư viện trống (Chưa chọn thư mục).";

                ShowToast("Thư viện nhạc đã được làm trống.");
                return;
            }

            try
            {
                _isScanning = true;
                OnPropertyChanged(nameof(IsNotScanning));
                StatusMessage = "Đang quét dữ liệu... Vui lòng đợi.";

                var folders = new List<string>(MusicFolders);
                await MusicManager.ScanMusic(folders);

                var newSongs = MusicManager.AllTracks;
                if (Application.Current.MainWindow.DataContext is MainViewModel mainVm)
                {
                    mainVm.RefreshData(newSongs.ToList());
                }

                StatusMessage = $"Hoàn tất! Tìm thấy {newSongs.Count} bài hát.";

            }
            catch (Exception ex)
            {
                StatusMessage = "Có lỗi xảy ra.";
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isScanning = false;
                OnPropertyChanged(nameof(IsNotScanning));
            }
        }

        private void SaveSettings(object parameter)
        {
            if (_isScanning) return;

            var settings = new AppSettings
            {
                CrossfadeSeconds = this.CrossfadeSeconds,
                IsMinimizeToTrayEnabled = this.IsMinimizeToTrayEnabled,
                MusicFolders = new List<string>(this.MusicFolders)
            };

            try
            {
                string jsonString = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, jsonString);

                StatusMessage = "Đã lưu cài đặt. Đang bắt đầu quét...";

                _ = RescanLibrary();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu: " + ex.Message);
            }
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    if (settings != null)
                    {
                        CrossfadeSeconds = settings.CrossfadeSeconds;
                        music4life.Services.MusicPlayer.CrossfadeDuration = settings.CrossfadeSeconds;
                        IsMinimizeToTrayEnabled = settings.IsMinimizeToTrayEnabled;

                        MusicFolders.Clear();
                        if (settings.MusicFolders != null)
                            foreach (var f in settings.MusicFolders) MusicFolders.Add(f);
                    }
                }
                catch { }
            }
        }
    }
}