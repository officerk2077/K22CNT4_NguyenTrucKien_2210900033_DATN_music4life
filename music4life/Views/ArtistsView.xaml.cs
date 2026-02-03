using System.Windows;
using System.Windows.Controls;
using music4life.ViewModels;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;
using Application = System.Windows.Application;

namespace music4life.Views
{
    public partial class ArtistsView : UserControl
    {
        public ArtistsView()
        {
            InitializeComponent();
        }

        private void ArtistCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ArtistInfo artist)
            {
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                if (mainWindow.DataContext is MainViewModel viewModel)
                {
                    viewModel.FilterSongsByArtist(artist.Name);

                    if (mainWindow.MainContent != null)
                    {
                        mainWindow.MainContent.Content = new SongListView();
                    }
                }
            }
        }
    }
}