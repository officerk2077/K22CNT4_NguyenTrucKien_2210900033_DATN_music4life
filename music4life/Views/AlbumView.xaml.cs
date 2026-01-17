using music4life.ViewModels;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;
using Application = System.Windows.Application;

namespace music4life.Views
{
    /// <summary>
    /// Interaction logic for AlbumView.xaml
    /// </summary>
    public partial class AlbumView : UserControl
    {
        public AlbumView()
        {
            InitializeComponent();
        }

        private void AlbumCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AlbumInfo album)
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (mainWindow.DataContext is MainViewModel viewModel)
                {
                    viewModel.FilterSongsByAlbum(album.Title);

                    if (mainWindow.MainContent != null)
                    {
                        mainWindow.MainContent.Content = new SongListView();
                    }
                }
            }
        }
    }
}