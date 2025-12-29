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
            TxtCateringCsv.Text = ConfigService.Current.Csv.PassengerData;
            TxtAvatarDbCsv.Text = ConfigService.Current.Csv.AvatarDb;
            TxtOrdersCsv.Text = ConfigService.Current.Csv.Orders;
            TxtBgImage.Text = ConfigService.Current.Paths.BGImage;
            TxtBoardingSound.Text = ConfigService.Current.Paths.BoardingSound;
            TxtCateringMusic.Text = ConfigService.Current.Paths.CateringMusic;
            TxtBoardingCountCsv.Text = ConfigService.Current.Csv.BoardingCount;
            TxtFrequentFlyerBg.Text = ConfigService.Current.Paths.FrequentFlyerBg;

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

        private void BrowseFile(TextBox target, string filter = "Alle Dateien|*.*")
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter
            };
            if (dlg.ShowDialog() == true)
                target.Text = dlg.FileName;
        }

        // --- Browse Buttons ---
        private void BrowseAvatars_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtAvatars);
        private void BrowseOrders_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtOrders);
        private void BrowseStewardess_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtStewardess);
        private void BrowseCabin_Click(object sender, RoutedEventArgs e) => BrowseFolder(TxtCabin);

        private void BrowseCateringCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtCateringCsv, "CSV Dateien|*.csv|Alle Dateien|*.*");
        private void BrowseAvatarDbCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtAvatarDbCsv, "CSV Dateien|*.csv|Alle Dateien|*.*");
        private void BrowseOrdersCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtOrdersCsv, "CSV Dateien|*.csv|Alle Dateien|*.*");
        private void BrowseBoardingSound_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtBoardingSound);
        private void BrowseCateringMusic_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtCateringMusic);
        private void BrowseBgImage_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtBgImage, "PNG Bilder|*.png|Alle Dateien|*.*");
        private void BrowseBoardingCountCsv_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtBoardingCountCsv, "CSV Dateien|*.csv|Alle Dateien|*.*");
        private void BrowseFrequentFlyerBg_Click(object sender, RoutedEventArgs e) => BrowseFile(TxtFrequentFlyerBg, "PNG Dateien|*.png|Alle Dateien|*.*");



        // --- Speichern ---
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Current.Paths.Avatars = TxtAvatars.Text;
            ConfigService.Current.Paths.Orders = TxtOrders.Text;
            ConfigService.Current.Paths.Stewardess = TxtStewardess.Text;
            ConfigService.Current.Paths.Cabin = TxtCabin.Text;
            ConfigService.Current.Csv.PassengerData = TxtCateringCsv.Text;
            ConfigService.Current.Csv.AvatarDb = TxtAvatarDbCsv.Text;
            ConfigService.Current.Csv.Orders = TxtOrdersCsv.Text;
            ConfigService.Current.Paths.BGImage = TxtBgImage.Text;
            ConfigService.Current.Paths.BoardingSound = TxtBoardingSound.Text;
            ConfigService.Current.Paths.CateringMusic = TxtCateringMusic.Text;
            ConfigService.Current.Csv.BoardingCount = TxtBoardingCountCsv.Text;
            ConfigService.Current.Paths.FrequentFlyerBg = TxtFrequentFlyerBg.Text;
            ConfigService.Save();

            MessageBox.Show("Einstellungen gespeichert.", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
