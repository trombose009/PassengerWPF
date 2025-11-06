using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf; // NuGet Paket "Ookii.Dialogs.Wpf" nötig

namespace CateringWPF
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            TxtAvatars.Text = ConfigService.Current.Paths.Avatars;
            TxtOrders.Text = ConfigService.Current.Paths.Orders;
            TxtStewardess.Text = ConfigService.Current.Paths.Stewardess;
            TxtCabin.Text = ConfigService.Current.Paths.Cabin;
            TxtCateringCsv.Text = ConfigService.Current.Csv.Catering;
            TxtAvatarDbCsv.Text = ConfigService.Current.Csv.AvatarDb;
        }

        private void BrowseFolder(TextBox target)
        {
            var dlg = new VistaFolderBrowserDialog
            {
                Description = "Bitte Ordner auswählen",
                ShowNewFolderButton = true
            };

            if (dlg.ShowDialog(this) == true)
                target.Text = dlg.SelectedPath;
        }

        private void BrowseFile(TextBox target)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV Dateien|*.csv|Alle Dateien|*.*"
            };
            if (dlg.ShowDialog() == true)
                target.Text = dlg.FileName;
        }

        private void BrowseAvatars_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtAvatars);
        private void BrowseOrders_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtOrders);
        private void BrowseStewardess_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtStewardess);
        private void BrowseCabin_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtCabin);
        private void BrowseCateringCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtCateringCsv);
        private void BrowseAvatarDbCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtAvatarDbCsv);

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Current.Paths.Avatars = TxtAvatars.Text;
            ConfigService.Current.Paths.Orders = TxtOrders.Text;
            ConfigService.Current.Paths.Stewardess = TxtStewardess.Text;
            ConfigService.Current.Paths.Cabin = TxtCabin.Text;
            ConfigService.Current.Csv.Catering = TxtCateringCsv.Text;
            ConfigService.Current.Csv.AvatarDb = TxtAvatarDbCsv.Text;

            ConfigService.Save();
            MessageBox.Show("Einstellungen gespeichert.", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
