using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace music4life.Views
{
    public partial class GenreDetailView : System.Windows.Controls.UserControl
    {
        public GenreDetailView()
        {
            InitializeComponent();
        }

        public GenreDetailView(string genreName, string hexColor) : this()
        {
            if (txtGenreTitle != null)
            {
                txtGenreTitle.Text = genreName;
            }

            if (HeaderGrid != null && !string.IsNullOrEmpty(hexColor))
            {
                try
                {
                    var mainColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                    var blackColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#121212");

                    var gradientBrush = new LinearGradientBrush();
                    gradientBrush.StartPoint = new System.Windows.Point(0, 0);
                    gradientBrush.EndPoint = new System.Windows.Point(0, 1);

                    gradientBrush.GradientStops.Add(new GradientStop(mainColor, 0.0));
                    gradientBrush.GradientStops.Add(new GradientStop(blackColor, 1.0));

                    HeaderGrid.Background = gradientBrush;
                }
                catch (Exception)
                {
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;

            if (mainWindow != null && mainWindow.MainContent != null)
            {
                mainWindow.MainContent.Content = new GenresView();
            }
        }
    }
}