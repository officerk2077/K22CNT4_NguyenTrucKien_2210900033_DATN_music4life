using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace music4life.Converters
{
    public class AlbumArtConverter : IValueConverter
    {
        private static Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string filePath = value as string;
            if (string.IsNullOrEmpty(filePath)) return null;

            if (_cache.TryGetValue(filePath, out var cachedImage)) return cachedImage;

            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    if (file.Tag.Pictures.Length >= 1)
                    {
                        var bin = file.Tag.Pictures[0].Data.Data;

                        var image = new BitmapImage();
                        image.BeginInit();
                        image.StreamSource = new MemoryStream(bin);
                        image.DecodePixelWidth = 200;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                        image.Freeze();

                        if (_cache.Count > 200) _cache.Clear();
                        _cache[filePath] = image;

                        return image;
                    }
                }
            }
            catch { }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}