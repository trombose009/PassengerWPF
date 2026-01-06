using SimConnect.NET;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
// Alias für Path, um Konflikte mit System.Windows.Shapes.Path zu vermeiden
using IOPath = System.IO.Path;

namespace PassengerWPF
{
    public partial class MainWindow : Window
    {
        // SimConnectClient aus SimConnect.NET
        private SimConnectClient simconnect;

        // Datei für den letzten Flug
        private readonly string lastFlightFile = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_flight.txt");

        public MainWindow()
        {
            InitializeComponent();
            // Timestamp vom letzten Flug aus Datei setzen
            TxtPassengerTimestampTab.Text = LoadLastFlightTimestamp();

            InitializeStartTab(); // Start-Tab erzeugen
        }

        #region Menu Actions

        private void OpenLiveBoarding(object sender, RoutedEventArgs e) => OpenTab("Live Boarding", () => new LiveBoardingControl());
        private void OpenCatering(object sender, RoutedEventArgs e) => OpenTab("Catering", () => new CateringControl());
        private void OpenAvatarMapping(object sender, RoutedEventArgs e) => OpenTab("Avatar Mapping", () => new AvatarMappingControl());
        private void OpenFrequentFlyer(object sender, RoutedEventArgs e) => OpenTab("Vielflieger", () => new FrequentFlyerControl());
        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }
        private void OpenObsOverlayTab(object sender, RoutedEventArgs e)
        {
            foreach (TabItem tab in MainTabs.Items)
            {
                if ((string)tab.Header == "Overlay / FlightData")
                {
                    MainTabs.SelectedItem = tab;
                    return;
                }
            }

            var overlayTab = new TabItem { Header = "Overlay / FlightData" };
            overlayTab.Content = new FlightDataOverlayControl();
            MainTabs.Items.Add(overlayTab);
            MainTabs.SelectedItem = overlayTab;
        }

        private void NewFlight_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Möchten Sie wirklich einen neuen Flug starten?\nAlle PassengerData werden zurückgesetzt.",
                "Neuen Flug starten", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // PassengerData zurücksetzen
            string passengerPath = ConfigService.Current.Csv.PassengerData;
            if (File.Exists(passengerPath))
                File.WriteAllText(passengerPath, "Name,Sitzplatz,Avatar,Order1,Order2,Order3,Order4");

            // LastFlightIds zurücksetzen
            ResetLastFlightIds();

            // Timestamp speichern
            SaveLastFlightTimestamp();
            UpdatePassengerDataTimestamp();

            MessageBox.Show(
                "Neuer Flug wurde gestartet.\nPassengerData wurde zurückgesetzt.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Tab Helper

        private void OpenTab(string header, Func<UIElement> contentFactory)
        {
            foreach (TabItem tab in MainTabs.Items)
            {
                if ((string)tab.Header == header)
                {
                    MainTabs.SelectedItem = tab;
                    return;
                }
            }

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
            foreach (TabItem existingTab in MainTabs.Items)
                if ((string)existingTab.Header == "Flugstatus") return;

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

          
            // --- "Neuen Flug starten" Button ---
            var btnNewFlight = new Button
            {
                Content = "Neuen Flug starten",
                Width = 250,
                Height = 45,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            btnNewFlight.Click += NewFlight_Click;
            stack.Children.Add(btnNewFlight);

            // --- "Verbindung zu SimConnect prüfen" Button ---
            var btnCheck = new Button
            {
                Content = "Verbindung zu SimConnect prüfen",
                Width = 250,
                Height = 45,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            btnCheck.Click += CheckSimConnectConnection_Click;
            stack.Children.Add(btnCheck);

            // --- SimConnect Statusanzeige ---
            var simStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var ellipse = new Ellipse
            {
                Name = "SimConnectIndicator",
                Width = 20,
                Height = 20,
                Fill = Brushes.Gray,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            simStack.Children.Add(ellipse);

            var txtSim = new TextBlock
            {
                Name = "SimConnectStatusText",
                Text = "SimConnect Status: Unbekannt",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            simStack.Children.Add(txtSim);

            stack.Children.Add(simStack);

            // --- Bedienungsanleitung ---
            var instructionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 20, 0, 0)
            };

            var instructionText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White
            };
            instructionText.Inlines.Add(new Run("⚠ Achtung: "));
            instructionText.Inlines.Add(new Run("Wenn Sie 'Neuen Flug starten' klicken, werden alle PassengerData zurückgesetzt und der letzte Flugzeitstempel aktualisiert. Bereits vergebene Sitzplatz-IDs werden geleert, sodass neue Passagiere wieder von vorne gezählt werden."));

            instructionBorder.Child = instructionText;
            stack.Children.Add(instructionBorder);

            border.Child = stack;

            var tabItem = new TabItem
            {
                Header = "Flugstatus",
                Content = border,
                IsSelected = true
            };

            MainTabs.Items.Insert(0, tabItem);
        }

        private void UpdatePassengerDataTimestamp()
        {
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
                txtStatus.Text = LoadLastFlightTimestamp();
        }

        private void ResetLastFlightIds()
        {
            string path = ConfigService.Current.Csv.BoardingCount;
            if (!File.Exists(path)) return;

            var lines = File.ReadAllLines(path).ToList();
            if (lines.Count <= 1) return;

            for (int i = 1; i < lines.Count; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length >= 3)
                {
                    parts[2] = "";
                    lines[i] = string.Join(",", parts);
                }
            }

            File.WriteAllLines(path, lines);
        }

        // ---------------------------
        // Letzten Flug Timestamp speichern und laden
        // ---------------------------
        private void SaveLastFlightTimestamp()
        {
            try
            {
                File.WriteAllText(lastFlightFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch { /* Fehler ignorieren */ }
        }

        private string LoadLastFlightTimestamp()
        {
            try
            {
                if (!File.Exists(lastFlightFile))
                    File.WriteAllText(lastFlightFile, "noch kein Flug gestartet");

                string timestamp = File.ReadAllText(lastFlightFile);
                return $"Letzter Start eines neuen Fluges: {timestamp}";
            }
            catch
            {
                return "Letzter Start eines neuen Fluges: ...";
            }
        }

        #endregion

        #region SimConnect Check

        private async void CheckSimConnectConnection_Click(object sender, RoutedEventArgs e)
        {
            SimConnectStatusText.Text = "Verbindungsstatus: Prüfen...";
            SimConnectIndicator.Fill = Brushes.Gray;

            try
            {
                if (simconnect != null)
                {
                    if (simconnect.IsConnected)
                        simconnect.Dispose();
                    simconnect = null;
                }

                simconnect = new SimConnectClient();
                await simconnect.ConnectAsync();

                if (simconnect.IsConnected)
                {
                    SimConnectStatusText.Text = "Verbindung hergestellt ✅";
                    SimConnectIndicator.Fill = Brushes.LimeGreen;
                }
                else
                {
                    SimConnectStatusText.Text = "Nicht verbunden ❌";
                    SimConnectIndicator.Fill = Brushes.Red;
                }
            }
            catch
            {
                SimConnectStatusText.Text = "Fehler beim Verbinden ❌";
                SimConnectIndicator.Fill = Brushes.Red;
            }
        }

        #endregion
    }
}
