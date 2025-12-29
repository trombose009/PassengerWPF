using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PassengerWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeStartTab(); // Start-Tab erzeugen
        }

        #region Menu Actions

        private void OpenLiveBoarding(object sender, RoutedEventArgs e)
        {
            OpenTab("Live Boarding", () => new LiveBoardingControl());
        }

        private void OpenCatering(object sender, RoutedEventArgs e)
        {
            OpenTab("Catering", () => new CateringControl());
        }

        private void OpenAvatarMapping(object sender, RoutedEventArgs e)
        {
            OpenTab("Avatar Mapping", () => new AvatarMappingControl());
        }

        private void OpenFrequentFlyer(object sender, RoutedEventArgs e)
        {
            OpenTab("Vielflieger", () => new FrequentFlyerControl());
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        private void NewFlight_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Möchten Sie wirklich einen neuen Flug starten?\nAlle PassengerData werden zurückgesetzt.",
                "Neuen Flug starten", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // PassengerData leeren
            string passengerPath = ConfigService.Current.Csv.PassengerData;
            if (File.Exists(passengerPath))
                File.WriteAllText(passengerPath, "Name,Sitzplatz,Avatar,Order1,Order2,Order3,Order4");

            // Timestamp aktualisieren
            UpdatePassengerDataTimestamp();

            MessageBox.Show(
                "Neuer Flug wurde gestartet.\nPassengerData wurde zurückgesetzt.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        #endregion

        #region Tab Helper

        private void OpenTab(string header, Func<UIElement> contentFactory)
        {
            // Prüfen, ob Tab bereits existiert
            foreach (TabItem tab in MainTabs.Items)
            {
                if ((string)tab.Header == header)
                {
                    MainTabs.SelectedItem = tab;
                    return;
                }
            }

            // Neues Tab erstellen
            var newTab = new TabItem
            {
                Header = header,
                Content = contentFactory()
            };

            MainTabs.Items.Add(newTab);
            MainTabs.SelectedItem = newTab;
        }

        #endregion

        #region Helper

        private void InitializeStartTab()
        {
            // Prüfen, ob das Tab schon existiert
            foreach (TabItem existingTab in MainTabs.Items)
            {
                if ((string)existingTab.Header == "Flugstatus") return;
            }

            // Border als Container, um Padding und abgerundete Ecken zu nutzen
            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            // StackPanel für vertikale Anordnung
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top
            };

            // TextBlock für PassengerData-Zeitstempel
            var txtStatus = new TextBlock
            {
                Name = "TxtPassengerTimestampTab",
                Text = GetPassengerDataTimestamp(),
                FontSize = 16,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(txtStatus);

            // Button für neuen Flug
            var btnNewFlight = new Button
            {
                Content = "Neuen Flug starten",
                Width = 180,
                Height = 45,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                Foreground = Brushes.White
            };
            btnNewFlight.Click += NewFlight_Click;
            stack.Children.Add(btnNewFlight);

            border.Child = stack;

            var tabItem = new TabItem
            {
                Header = "Flugstatus",
                Content = border,
                IsSelected = true
            };

            MainTabs.Items.Insert(0, tabItem); // Immer an erster Stelle
        }


        private void UpdatePassengerDataTimestamp()
        {
            // Start-Tab finden
            var tab = MainTabs.Items.OfType<TabItem>()
                        .FirstOrDefault(t => (string)t.Header == "Flugstatus");
            if (tab == null) return;

            var border = tab.Content as Border;
            if (border == null) return;

            var stack = border.Child as StackPanel;
            if (stack == null) return;

            var txtStatus = stack.Children.OfType<TextBlock>()
                              .FirstOrDefault(tb => tb.Name == "TxtPassengerTimestampTab");

            if (txtStatus != null)
            {
                string path = ConfigService.Current.Csv.PassengerData;
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    txtStatus.Text = $"Letzter Start eines neuen Fluges: {info.LastWriteTime}";
                }
                else
                {
                    txtStatus.Text = "PassengerData-Datei existiert nicht.";
                }
            }
        }


        private string GetPassengerDataTimestamp()
        {
            string path = ConfigService.Current.Csv.PassengerData;
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return $"Letzter Start eines neuen Fluges: {info.LastWriteTime}";
            }
            return "PassengerData-Datei existiert nicht.";
        }

        #endregion
    }
}
