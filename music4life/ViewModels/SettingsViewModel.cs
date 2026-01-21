using music4life.Models;
using music4life.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using WpfApp = System.Windows.Application;

namespace music4life.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private double _crossfadeSeconds;
        private bool _isMinimizeToTrayEnabled;
        private bool _isScanning;

        private string _statusMessage;

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
        public ICommand CloseCommand { get; set; }

        public SettingsViewModel()
        {
            MusicFolders = new ObservableCollection<string>();
            _isScanning = false;
            StatusMessage = "Sẵn sàng";

            LoadSettings();

            AddFolderCommand = new RelayCommand<object>((p) => AddFolder());
            RemoveFolderCommand = new RelayCommand<string>((path) => RemoveFolder(path));

            SaveSettingsCommand = new RelayCommand<object>((p) => SaveSettings(p));

            RescanCommand = new RelayCommand<object>(async (p) => await RescanLibrary());
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

                        System.Windows.MessageBox.Show(
                            "Đã thêm thư mục thành công!\n\nVui lòng bấm nút 'Lưu Cài Đặt' để cập nhật.",
                            "Thông báo",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Thư mục này đã có trong danh sách rồi!", "Trùng lặp");
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
                if (WpfApp.Current.MainWindow.DataContext is MainViewModel mainVm)
                {
                    mainVm.RefreshData(new List<Song>());
                }

                music4life.Services.MusicPlayer.CurrentPlaylist.Clear();
                MusicManager.AllTracks.Clear();

                StatusMessage = "Thư viện trống (Chưa chọn thư mục).";
                System.Windows.MessageBox.Show("Đã xoá hết thư mục. Thư viện nhạc đã được làm trống.", "Thông báo");
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

                if (WpfApp.Current.MainWindow.DataContext is MainViewModel mainVm)
                {
                    mainVm.RefreshData(newSongs.ToList());
                }

                StatusMessage = $"Hoàn tất! Tìm thấy {newSongs.Count} bài hát.";
                System.Windows.MessageBox.Show($"Đã quét xong! Tìm thấy {newSongs.Count} bài hát.", "Thành công");
            }
            catch (Exception ex)
            {
                StatusMessage = "Có lỗi xảy ra.";
                System.Windows.MessageBox.Show("Có lỗi xảy ra: " + ex.Message, "Lỗi");
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
                File.WriteAllText("settings.json", jsonString);

                StatusMessage = "Đã lưu cài đặt. Đang bắt đầu quét...";

                _ = RescanLibrary();

                if (parameter is System.Windows.Window window)
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Lỗi khi lưu: " + ex.Message);
            }
        }

        private void LoadSettings()
        {
            if (File.Exists("settings.json"))
            {
                try
                {
                    string jsonString = File.ReadAllText("settings.json");
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