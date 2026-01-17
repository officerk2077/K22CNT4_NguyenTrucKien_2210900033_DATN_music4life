using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace music4life.Views
{
    public partial class GenresView : System.Windows.Controls.UserControl
    {
        public GenresView()
        {
            InitializeComponent();
        }

        private void GenreCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Border card)
            {
                var textBlock = card.Child as TextBlock;
                string genreName = textBlock?.Text ?? "Genre";

                string colorCode = card.Tag?.ToString() ?? "#333333";

                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;

                if (mainWindow != null && mainWindow.MainContent != null)
                {
                    var detailView = new GenreDetailView(genreName, colorCode);

                    mainWindow.MainContent.Content = detailView;
                }
            }
        }
    }
}