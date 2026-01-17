using System.Windows;
using System.Windows.Input;

namespace music4life.Views
{
    public partial class CreatePlaylistWindow : Window
    {
        public string CreatedPlaylistName { get; private set; }

        public CreatePlaylistWindow()
        {
            InitializeComponent();

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtPlaylistName.Text.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                CreatedPlaylistName = name;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Vui lòng nhập tên Playlist!", "Thông báo");
            }
        }
    }
}