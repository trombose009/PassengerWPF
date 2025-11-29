using System.IO;
using System.Windows;

namespace PassengerWPF
{
    public partial class MainMenuWindow : Window
    {
        public MainMenuWindow()
        {
            InitializeComponent();
            Loaded += MainMenuWindow_Loaded;
        }

        // Wird ausgeführt, sobald das Fenster sichtbar ist → Owner kann gesetzt werden
        private void MainMenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Config laden
                ConfigService.Load();

                // PassengerData-Zeitstempel aktualisieren
                UpdatePassengerDataTimestamp();

                // Pfade prüfen
                EnsureRequiredPathsSet();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Initialisieren:\n{ex.Message}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }


        #region Button Clicks

        private void BtnCatering_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRequiredPathsSet()) return;

            try
            {
                var win = new CateringWindow { Owner = this };
                win.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Starten des Catering-Fensters:\n{ex.Message}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow { Owner = this };
            settingsWin.ShowDialog();

            // Falls Pfade geändert wurden: sofort aktualisieren
            UpdatePassengerDataTimestamp();
        }

        private void BtnAvatarMapping_Click(object sender, RoutedEventArgs e)
        {
            var win = new AvatarMappingWindow { Owner = this };
            win.Show();
        }

        private void BtnLiveBoarding_Click(object sender, RoutedEventArgs e)
        {
            var win = new LiveBoardingWindow { Owner = this };
            win.Show();
        }

        private void BtnClearPassengerData_Click(object sender, RoutedEventArgs e)
        {
            string path = ConfigService.Current.Csv.PassengerData;

            if (!File.Exists(path))
            {
                MessageBox.Show("PassengerData-Datei existiert nicht.", "Info",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Möchten Sie die PassengerData-Datei wirklich leeren?",
                                         "PassengerData leeren",
                                         MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                File.WriteAllText(path, "Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
                UpdatePassengerDataTimestamp();

                MessageBox.Show("PassengerData-Datei wurde geleert.", "Info",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion


        #region Helper

        /// <summary>
        /// Prüft die Pfade; falls sie nicht gesetzt sind → Settings öffnen.
        /// </summary>
        private bool EnsureRequiredPathsSet()
        {
            if (ArePathsSet())
                return true;

            MessageBox.Show("Bitte zuerst die Pfade in den Einstellungen setzen.",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);

            var settingsWin = new SettingsWindow { Owner = this };
            settingsWin.ShowDialog();

            if (!ArePathsSet())
            {
                MessageBox.Show("Die Pfade wurden nicht gesetzt. Das Programm wird beendet.",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return false;
            }

            return true;
        }


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
