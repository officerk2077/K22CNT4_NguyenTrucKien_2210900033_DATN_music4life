using System.Windows;
using System.Windows.Input;
using music4life.ViewModels;

namespace music4life.Views
{
    public partial class SettingWindow : Window
    {
        public SettingWindow()
        {
            InitializeComponent();

            this.DataContext = new SettingsViewModel();

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    this.DragMove();
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SettingsViewModel vm)
            {
                if (vm.SaveSettingsCommand != null && vm.SaveSettingsCommand.CanExecute(null))
                {
                    vm.SaveSettingsCommand.Execute(null);
                }
            }

            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                mw.ShowToast("Đã lưu cài đặt thành công!");
            }

            this.Close();
        }
    }
}