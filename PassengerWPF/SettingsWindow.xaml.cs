using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf; // NuGet Paket "Ookii.Dialogs.Wpf" nötig

namespace PassengerWPF
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // Vorhandene Werte aus Config laden
            TxtAvatars.Text = ConfigService.Current.Paths.Avatars;
            TxtOrders.Text = ConfigService.Current.Paths.Orders;
            TxtStewardess.Text = ConfigService.Current.Paths.Stewardess;
            TxtCabin.Text = ConfigService.Current.Paths.Cabin;
            TxtCateringCsv.Text = ConfigService.Current.Csv.PassengerData; // geändert
            TxtAvatarDbCsv.Text = ConfigService.Current.Csv.AvatarDb;
            TxtOrdersCsv.Text = ConfigService.Current.Csv.Orders;
            TxtActualFlightCsv.Text = ConfigService.Current.Csv.ActualFlight; // neu
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

        // --- Browse Buttons ---
        private void BrowseAvatars_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtAvatars);
        private void BrowseOrders_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtOrders);
        private void BrowseStewardess_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtStewardess);
        private void BrowseCabin_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtCabin);
        private void BrowseCateringCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtCateringCsv);
        private void BrowseAvatarDbCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtAvatarDbCsv);
        private void BrowseOrdersCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtOrdersCsv);

        private void BrowseActualFlightCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtActualFlightCsv); // neu

        // --- Speichern ---
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Current.Paths.Avatars = TxtAvatars.Text;
            ConfigService.Current.Paths.Orders = TxtOrders.Text;
            ConfigService.Current.Paths.Stewardess = TxtStewardess.Text;
            ConfigService.Current.Paths.Cabin = TxtCabin.Text;
            ConfigService.Current.Csv.PassengerData = TxtCateringCsv.Text; // geändert
            ConfigService.Current.Csv.AvatarDb = TxtAvatarDbCsv.Text;
            ConfigService.Current.Csv.Orders = TxtOrdersCsv.Text;
            ConfigService.Current.Csv.ActualFlight = TxtActualFlightCsv.Text; // neu

            ConfigService.Save();

            MessageBox.Show("Einstellungen gespeichert.", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
