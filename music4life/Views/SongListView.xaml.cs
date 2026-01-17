using System.Linq;
using System.Windows;
using System.Windows.Input;
using music4life.Models;
using music4life.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using ListViewItem = System.Windows.Controls.ListViewItem;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace music4life.Views
{
    public partial class SongListView : UserControl
    {
        public SongListView()
        {
            InitializeComponent();

        }

        private void SongItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item && item.Content is Song selectedSong)
            {
                if (this.DataContext is MainViewModel vm)
                {
                    if (vm.PlaySongCommand.CanExecute(selectedSong))
                    {
                        vm.PlaySongCommand.Execute(selectedSong);
                    }
                }
            }
        }

        private void SortOption_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBoxItem item && item.Tag != null)
            {
                if (this.DataContext is MainViewModel vm)
                {
                    vm.ApplySort(item.Tag.ToString());

                    if (MusicListView != null)
                    {
                        MusicListView.Items.Refresh();
                    }
                }
            }
        }
        private void ListViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null)
            {
                item.IsSelected = true;
                item.Focus();

            }
        }
    }
}