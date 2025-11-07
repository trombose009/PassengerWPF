using System;
using System.IO;
using System.Windows;

namespace CateringWPF
{
    public partial class MainMenuWindow : Window
    {
        public MainMenuWindow()
        {
            InitializeComponent();

            // Config beim Start laden
            ConfigService.Load();

            // Catering-Datei Zeitstempel anzeigen
            UpdateCateringTimestamp();
        }

        #region Button Clicks

        private void BtnCatering_Click(object sender, RoutedEventArgs e)
        {
            // Prüfen ob Pfade gesetzt sind
            if (string.IsNullOrEmpty(ConfigService.Current.Paths.Avatars) ||
                string.IsNullOrEmpty(ConfigService.Current.Paths.Cabin) ||
                string.IsNullOrEmpty(ConfigService.Current.Paths.Stewardess) ||
                string.IsNullOrEmpty(ConfigService.Current.Csv.Catering))
            {
                MessageBox.Show("Bitte zuerst die Pfade in den Einstellungen setzen.",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // CateringWindow erzeugen und öffnen, MainMenuWindow bleibt offen
                var cateringWin = new CateringWindow
                {
                    Owner = this // optional für modale Verknüpfung
                };
                cateringWin.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Starten des Catering-Fensters:\n{ex.Message}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow
            {
                Owner = this
            };
            settingsWin.ShowDialog();
        }

        private void BtnAvatarMapping_Click(object sender, RoutedEventArgs e)
        {
            var avatarWindow = new AvatarMappingWindow
            {
                Owner = this
            };
            avatarWindow.Show();
        }

        private void BtnClearCatering_Click(object sender, RoutedEventArgs e)
        {
            string path = ConfigService.Current.Csv.Catering;
            if (!File.Exists(path))
            {
                MessageBox.Show("Catering-Datei existiert nicht.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Möchten Sie die Catering-Datei wirklich leeren?",
                                         "Catering leeren", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                // Datei mit Header neu erstellen
                File.WriteAllText(path, "Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
                UpdateCateringTimestamp();
                MessageBox.Show("Catering-Datei wurde geleert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Helper

        private void UpdateCateringTimestamp()
        {
            string path = ConfigService.Current.Csv.Catering;
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                TxtCateringTimestamp.Text = $"Letzte Änderung: {info.LastWriteTime}";
            }
            else
            {
                TxtCateringTimestamp.Text = "Catering-Datei existiert nicht.";
            }
        }

        #endregion
    }
}