using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf; // NuGet Paket "Ookii.Dialogs.Wpf" nötig

namespace PassengerWPF
{
    public partial class SettingsWindow : Window
    {
        private readonly string exeRoot = AppDomain.CurrentDomain.BaseDirectory;

        public SettingsWindow()
        {
            InitializeComponent();

            // --- Standardpfade relativ zur EXE ---
            string defaultAvatars = Path.Combine("images", "avatars");
            string defaultOrders = Path.Combine("images", "orders");
            string defaultStewardess = Path.Combine("images", "stewardess");
            string defaultCabin = Path.Combine("images", "cabin");
            string defaultPassengerCsv = "PassengerData.csv";
            string defaultAvatarDbCsv = "AvatarDb.csv";
            string defaultOrdersCsv = "Orders.csv";
            string defaultBgImage = Path.Combine("stuff", "BGImage.png");
            string defaultBoardingSound = Path.Combine("stuff", "bingbing.wav");
            string defaultCateringMusic = Path.Combine("stuff", "CateringMusic.mp3");
            string defaultBoardingCountCsv = "boarding_count.csv";
            string defaultFrequentFlyerBg = Path.Combine("stuff", "FrequentFlyerBg.png");

            // --- Config vorbelegen, falls leer ---
            var paths = ConfigService.Current.Paths;
            var csv = ConfigService.Current.Csv;

            paths.Avatars = string.IsNullOrEmpty(paths.Avatars) ? PathHelpers.MakeAbsolute(defaultAvatars) : paths.Avatars;
            paths.Orders = string.IsNullOrEmpty(paths.Orders) ? PathHelpers.MakeAbsolute(defaultOrders) : paths.Orders;
            paths.Stewardess = string.IsNullOrEmpty(paths.Stewardess) ? PathHelpers.MakeAbsolute(defaultStewardess) : paths.Stewardess;
            paths.Cabin = string.IsNullOrEmpty(paths.Cabin) ? PathHelpers.MakeAbsolute(defaultCabin) : paths.Cabin;
            csv.PassengerData = string.IsNullOrEmpty(csv.PassengerData) ? PathHelpers.MakeAbsolute(defaultPassengerCsv) : csv.PassengerData;
            csv.AvatarDb = string.IsNullOrEmpty(csv.AvatarDb) ? PathHelpers.MakeAbsolute(defaultAvatarDbCsv) : csv.AvatarDb;
            csv.Orders = string.IsNullOrEmpty(csv.Orders) ? PathHelpers.MakeAbsolute(defaultOrdersCsv) : csv.Orders;
            paths.BGImage = string.IsNullOrEmpty(paths.BGImage) ? PathHelpers.MakeAbsolute(defaultBgImage) : paths.BGImage;
            paths.BoardingSound = string.IsNullOrEmpty(paths.BoardingSound) ? PathHelpers.MakeAbsolute(defaultBoardingSound) : paths.BoardingSound;
            paths.CateringMusic = string.IsNullOrEmpty(paths.CateringMusic) ? PathHelpers.MakeAbsolute(defaultCateringMusic) : paths.CateringMusic;
            csv.BoardingCount = string.IsNullOrEmpty(csv.BoardingCount) ? PathHelpers.MakeAbsolute(defaultBoardingCountCsv) : csv.BoardingCount;
            paths.FrequentFlyerBg = string.IsNullOrEmpty(paths.FrequentFlyerBg) ? PathHelpers.MakeAbsolute(defaultFrequentFlyerBg) : paths.FrequentFlyerBg;

            // --- Textfelder aktualisieren ---
            TxtAvatars.Text = paths.Avatars;
            TxtOrders.Text = paths.Orders;
            TxtStewardess.Text = paths.Stewardess;
            TxtCabin.Text = paths.Cabin;
            TxtCateringCsv.Text = csv.PassengerData;
            TxtAvatarDbCsv.Text = csv.AvatarDb;
            TxtOrdersCsv.Text = csv.Orders;
            TxtBgImage.Text = paths.BGImage;
            TxtBoardingSound.Text = paths.BoardingSound;
            TxtCateringMusic.Text = paths.CateringMusic;
            TxtBoardingCountCsv.Text = csv.BoardingCount;
            TxtFrequentFlyerBg.Text = paths.FrequentFlyerBg;

            // --- Ordner erstellen, falls nicht vorhanden ---
            EnsureFolderExists(paths.Avatars);
            EnsureFolderExists(paths.Orders);
            EnsureFolderExists(paths.Stewardess);
            EnsureFolderExists(paths.Cabin);
            EnsureFolderExists(Path.GetDirectoryName(paths.BGImage));
            EnsureFolderExists(Path.GetDirectoryName(paths.BoardingSound));
            EnsureFolderExists(Path.GetDirectoryName(paths.CateringMusic));
            EnsureFolderExists(Path.GetDirectoryName(paths.FrequentFlyerBg));
        }

        private void EnsureFolderExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // --- Browse Methoden ---
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
            var dlg = new OpenFileDialog { Filter = filter };
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
            var paths = ConfigService.Current.Paths;
            var csv = ConfigService.Current.Csv;

            // --- Absolutpfade in Config konvertieren ---
            paths.Avatars = PathHelpers.MakeAbsolute(TxtAvatars.Text);
            paths.Orders = PathHelpers.MakeAbsolute(TxtOrders.Text);
            paths.Stewardess = PathHelpers.MakeAbsolute(TxtStewardess.Text);
            paths.Cabin = PathHelpers.MakeAbsolute(TxtCabin.Text);
            paths.BGImage = PathHelpers.MakeAbsolute(TxtBgImage.Text);
            paths.BoardingSound = PathHelpers.MakeAbsolute(TxtBoardingSound.Text);
            paths.CateringMusic = PathHelpers.MakeAbsolute(TxtCateringMusic.Text);
            paths.FrequentFlyerBg = PathHelpers.MakeAbsolute(TxtFrequentFlyerBg.Text);

            csv.PassengerData = PathHelpers.MakeAbsolute(TxtCateringCsv.Text);
            csv.AvatarDb = PathHelpers.MakeAbsolute(TxtAvatarDbCsv.Text);
            csv.Orders = PathHelpers.MakeAbsolute(TxtOrdersCsv.Text);
            csv.BoardingCount = PathHelpers.MakeAbsolute(TxtBoardingCountCsv.Text);

            ConfigService.Save();

            MessageBox.Show("Einstellungen gespeichert.", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
        // --- Zurücksetzen auf Standardwerte ---
        private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            // Standardpfade relativ zur EXE
            string defaultAvatars = Path.Combine("images", "avatars");
            string defaultOrders = Path.Combine("images", "orders");
            string defaultStewardess = Path.Combine("images", "stewardess");
            string defaultCabin = Path.Combine("images", "cabin");
            string defaultPassengerCsv = "PassengerData.csv";
            string defaultAvatarDbCsv = "AvatarDb.csv";
            string defaultOrdersCsv = "orders.csv";
            string defaultBgImage = Path.Combine("stuff", "BGImage.png");
            string defaultBoardingSound = Path.Combine("stuff", "bingbing.wav");
            string defaultCateringMusic = Path.Combine("stuff", "CateringMusic.mp3");
            string defaultBoardingCountCsv = "boarding_count.csv";
            string defaultFrequentFlyerBg = Path.Combine("stuff", "FrequentFlyerBg.png");

            // --- Nur Textfelder füllen, Config wird noch nicht verändert ---
            TxtAvatars.Text = PathHelpers.MakeAbsolute(defaultAvatars);
            TxtOrders.Text = PathHelpers.MakeAbsolute(defaultOrders);
            TxtStewardess.Text = PathHelpers.MakeAbsolute(defaultStewardess);
            TxtCabin.Text = PathHelpers.MakeAbsolute(defaultCabin);
            TxtBgImage.Text = PathHelpers.MakeAbsolute(defaultBgImage);
            TxtBoardingSound.Text = PathHelpers.MakeAbsolute(defaultBoardingSound);
            TxtCateringMusic.Text = PathHelpers.MakeAbsolute(defaultCateringMusic);
            TxtFrequentFlyerBg.Text = PathHelpers.MakeAbsolute(defaultFrequentFlyerBg);

            TxtCateringCsv.Text = PathHelpers.MakeAbsolute(defaultPassengerCsv);
            TxtAvatarDbCsv.Text = PathHelpers.MakeAbsolute(defaultAvatarDbCsv);
            TxtOrdersCsv.Text = PathHelpers.MakeAbsolute(defaultOrdersCsv);
            TxtBoardingCountCsv.Text = PathHelpers.MakeAbsolute(defaultBoardingCountCsv);

            MessageBox.Show("Alle Pfade wurden auf die Standardwerte zurückgesetzt.\nZum Speichern bitte auf 'Speichern' klicken.",
                            "Zurückgesetzt", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}
