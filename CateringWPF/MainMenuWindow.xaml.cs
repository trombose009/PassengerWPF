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
        }

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
                var cateringWin = new CateringWindow();
                cateringWin.Owner = this; // optional für modale Verknüpfung
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
            var settingsWin = new SettingsWindow
            {
                Owner = this
            };
            settingsWin.ShowDialog();
        }
        private void BtnAvatarMapping_Click(object sender, RoutedEventArgs e)
        {
            var avatarWindow = new AvatarMappingWindow();
            avatarWindow.Owner = this;
            avatarWindow.Show();
        }

    }
}
