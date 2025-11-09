using System.IO;
using System.Windows;

namespace PassengerWPF
{
    public partial class MainMenuWindow : Window
    {
        public MainMenuWindow()
        {
            InitializeComponent();

            // Config laden
            ConfigService.Load();

            // PassengerData-Zeitstempel aktualisieren
            UpdatePassengerDataTimestamp();

            // Prüfen, ob wichtige Pfade gesetzt sind
            if (!ArePathsSet())
            {
                MessageBox.Show("Bitte zuerst die Pfade in den Einstellungen setzen.",
                                "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);

                // Einstellungen sofort öffnen
                var settingsWin = new SettingsWindow();
                settingsWin.ShowDialog();

                // Nach dem Schließen erneut prüfen
                if (!ArePathsSet())
                {
                    MessageBox.Show("Die Pfade wurden nicht gesetzt. Das Programm wird beendet.",
                                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
        }

        #region Button Clicks

        private void BtnCatering_Click(object sender, RoutedEventArgs e)
        {
            if (!ArePathsSet())
            {
                MessageBox.Show("Bitte zuerst die Pfade in den Einstellungen setzen.",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var cateringWin = new CateringWindow();
                cateringWin.Owner = this;   // HIER ok -> MainMenu ist sichtbar
                cateringWin.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Starten des Catering-Fensters:\n{ex.Message}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow();
            settingsWin.Owner = this;
            settingsWin.ShowDialog();
        }

        private void BtnAvatarMapping_Click(object sender, RoutedEventArgs e)
        {
            var avatarWin = new AvatarMappingWindow();
            avatarWin.Owner = this;
            avatarWin.Show();
        }

        private void BtnClearPassengerData_Click(object sender, RoutedEventArgs e)
        {
            string path = ConfigService.Current.Csv.PassengerData;
            if (!File.Exists(path))
            {
                MessageBox.Show("PassengerData-Datei existiert nicht.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Möchten Sie die PassengerData-Datei wirklich leeren?",
                                         "PassengerData leeren", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                File.WriteAllText(path, "Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
                UpdatePassengerDataTimestamp();
                MessageBox.Show("PassengerData-Datei wurde geleert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Helper

        private void UpdatePassengerDataTimestamp()
        {
            string path = ConfigService.Current.Csv.PassengerData;
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                TxtPassengerTimestamp.Text = $"Letzte Änderung: {info.LastWriteTime}";
            }
            else
            {
                TxtPassengerTimestamp.Text = "PassengerData-Datei existiert nicht.";
            }
        }

        private bool ArePathsSet()
        {
            var p = ConfigService.Current.Paths;
            var c = ConfigService.Current.Csv;

            return !string.IsNullOrEmpty(p.Avatars) &&
                   !string.IsNullOrEmpty(p.Cabin) &&
                   !string.IsNullOrEmpty(p.Stewardess) &&
                   !string.IsNullOrEmpty(c.PassengerData);
        }

        #endregion
    }
}
