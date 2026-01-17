using music4life.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace music4life.Views
{
    public partial class ArtistsView : System.Windows.Controls.UserControl
    {
        public ArtistsView()
        {
            InitializeComponent();
        }

        private void ArtistCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ArtistInfo artist)
            {
                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;

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