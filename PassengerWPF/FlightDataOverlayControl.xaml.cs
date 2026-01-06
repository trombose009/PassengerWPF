using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SimConnect.NET;

namespace PassengerWPF
{
    public partial class FlightDataOverlayControl : UserControl
    {
        private DispatcherTimer updateTimer;
        private DispatcherTimer rotationTimer;
        private DispatcherTimer passengerTimer;
        private int currentPageIndex = -1;

        private SimConnectClient simconnect;
        private WebServer webServer;

        // === Zentrale Pfade ===
        private string appRoot => AppDomain.CurrentDomain.BaseDirectory;
        private string stuffPath => Path.Combine(appRoot, "stuff");
        private string seatMapPath => Path.Combine(stuffPath, "boarding_render.png");

        // Live-Daten
        public static double CurrentAltitude { get; private set; }
        public static double CurrentSpeed { get; private set; }
        public static double CurrentHeading { get; private set; }
        public static double CurrentLat { get; private set; }
        public static double CurrentLon { get; private set; }
        public static double VSpeed { get; private set; }

        // Dynamische Passagierliste
        public static ObservableCollection<PassengerViewModel> OverlayPassengers { get; } = new ObservableCollection<PassengerViewModel>();
        private Queue<PassengerViewModel> passengerQueue = new Queue<PassengerViewModel>();

        // ===============================
        // Statische Getter für WebServer
        // ===============================
        public static bool ShowManualAircraftStatic { get; private set; } = false;
        public static bool ShowManualDepStatic { get; private set; } = false;
        public static bool ShowManualArrStatic { get; private set; } = false;

        public static string AircraftTypeValueStatic { get; private set; } = "B737";
        public static string DepValueStatic { get; private set; } = "EDDF";
        public static string ArrValueStatic { get; private set; } = "EDDM";

        // ===============================
        // Konstruktor
        // ===============================
        public FlightDataOverlayControl()
        {
            InitializeComponent();
            DataContext = this;

            Directory.CreateDirectory(stuffPath);

            // --- Standard Rotationsintervall 10 Sekunden ---
            int defaultRotation = ConfigService.Current?.Paths?.Overlay?.RotationIntervalSeconds ?? 10;
            TxtRotationInterval.Text = defaultRotation.ToString();
            rotationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(defaultRotation)
            };
            rotationTimer.Tick += (s, e) => RotatePages();

            // --- Event-Handler für Flugdaten-Untermenü ---
            ChkFlightData.Checked += ChkFlightData_Checked;
            ChkFlightData.Unchecked += ChkFlightData_Unchecked;

            // --- Passagiere initial in Queue ---
            foreach (var p in PassengerStore.Passengers)
                passengerQueue.Enqueue(p);

            // Neue Passagiere automatisch enqueue
            PassengerStore.Passengers.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (PassengerViewModel p in e.NewItems)
                        passengerQueue.Enqueue(p);
                }
            };

            // --- Timer: stufenweises Anzeigen der Passagiere ---
            passengerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            passengerTimer.Tick += (s, e) =>
            {
                if (passengerQueue.Count > 0)
                    OverlayPassengers.Add(passengerQueue.Dequeue());
            };

            // --- Timer: Flugdaten ---
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += async (s, e) => await UpdateFlightDataAsync();

            // --- Loaded Event ---
            Loaded += FlightDataOverlayControl_Loaded;
        }

        private void FlightDataOverlayControl_Loaded(object sender, RoutedEventArgs e)
        {
            StartService();
        }

        // ===============================
        // Checkbox-Handler für Untermenü
        // ===============================
        private void ChkFlightData_Checked(object sender, RoutedEventArgs e)
        {
            if (FlightDataMenu != null)
                FlightDataMenu.Visibility = Visibility.Visible;
        }

        private void ChkFlightData_Unchecked(object sender, RoutedEventArgs e)
        {
            if (FlightDataMenu != null)
                FlightDataMenu.Visibility = Visibility.Collapsed;
        }

        // ===============================
        // Service-Start / Stop
        // ===============================
        public async void StartService()
        {
            try
            {
                await InitSimConnectAsync();

                updateTimer.Start();
                rotationTimer.Start();
                passengerTimer.Start();

                // Webserver starten
                webServer?.Stop();
                webServer = new WebServer();
                webServer.Start();

                WebServerUrlTextBlock.Text =
                    $"URL: http://localhost:{ConfigService.Current?.Paths?.Overlay?.ServerPort ?? 8080}/FlightOverlay.html";

                OverlayLog.Text += "Service gestartet\n";

                RefreshSeatMap();
                await InitMapAsync();
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim StartService: {ex.Message}\n";
            }
        }

        public void StopService()
        {
            updateTimer.Stop();
            rotationTimer.Stop();
            passengerTimer.Stop();
            simconnect?.Dispose();
            webServer?.Stop();
            OverlayLog.Text += "Service gestoppt\n";
        }

        // ===============================
        // SimConnect-Daten abrufen
        // ===============================
        private async Task InitSimConnectAsync()
        {
            try
            {
                simconnect = new SimConnectClient();
                await simconnect.ConnectAsync();
                OverlayLog.Text += "SimConnect erfolgreich verbunden\n";
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"SimConnect Init Fehler: {ex.Message}\n";
            }
        }

        private async Task UpdateFlightDataAsync()
        {
            try
            {
                if (simconnect != null && simconnect.IsConnected)
                {
                    CurrentAltitude = await simconnect.SimVars.GetAsync<double>("PLANE ALTITUDE", "feet");
                    CurrentSpeed = await simconnect.SimVars.GetAsync<double>("AIRSPEED INDICATED", "knots");
                    CurrentHeading = await simconnect.SimVars.GetAsync<double>("PLANE HEADING DEGREES TRUE", "degrees");
                    CurrentLat = await simconnect.SimVars.GetAsync<double>("PLANE LATITUDE", "degrees");
                    CurrentLon = await simconnect.SimVars.GetAsync<double>("PLANE LONGITUDE", "degrees");
                    VSpeed = await simconnect.SimVars.GetAsync<double>("VERTICAL SPEED", "feet per minute");
                }

                // ===============================
                // Update statische Werte für WebServer
                // ===============================
                ShowManualAircraftStatic = ChkManualAircraft?.IsChecked == true;
                ShowManualDepStatic = ChkManualDep?.IsChecked == true;
                ShowManualArrStatic = ChkManualArr?.IsChecked == true;

                AircraftTypeValueStatic = TxtAircraftType?.Text ?? "B737";
                DepValueStatic = TxtDep?.Text ?? "EDDF";
                ArrValueStatic = TxtArr?.Text ?? "EDDM";

                // ===============================
                // Vorschau dynamisch zusammenstellen
                // ===============================
                var sb = new StringBuilder();

                if (ChkShowAltitude.IsChecked == true)
                    sb.AppendLine($"Altitude: {CurrentAltitude:F0} ft");
                if (ChkShowSpeed.IsChecked == true)
                    sb.AppendLine($"Speed: {CurrentSpeed:F0} kt");
                if (ChkShowHeading.IsChecked == true)
                    sb.AppendLine($"Heading: {CurrentHeading:F0}°");
                if (ChkShowPosition.IsChecked == true)
                    sb.AppendLine($"Position: {CurrentLat:F6}, {CurrentLon:F6}");
                if (ChkShowVSpeed.IsChecked == true)
                    sb.AppendLine($"VSpeed: {VSpeed:F0} ft/min");

                if (ChkManualAircraft.IsChecked == true)
                    sb.AppendLine($"Aircraft Type: {TxtAircraftType.Text}");
                if (ChkManualDep.IsChecked == true)
                    sb.AppendLine($"DEP: {TxtDep.Text}");
                if (ChkManualArr.IsChecked == true)
                    sb.AppendLine($"ARR: {TxtArr.Text}");

                PreviewTextBlock.Text = sb.ToString();

                // Map aktualisieren
                if (FlightMapWebView?.CoreWebView2 != null)
                    await FlightMapWebView.CoreWebView2.ExecuteScriptAsync($"updatePlane({CurrentLat}, {CurrentLon});");
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim Abrufen der Flugdaten: {ex.Message}\n";
            }
        }

        // ===============================
        // Overlay-Rotation
        // ===============================
        private void RotatePages()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RotatePages);
                return;
            }

            var pages = new List<Border>();

            if (ChkFlightData?.IsChecked == true) pages.Add(FlightDataPanel);
            if (ChkMap?.IsChecked == true) pages.Add(MapPanel);
            if (ChkPassenger?.IsChecked == true) pages.Add(PassengerPanel);
            if (ChkBoarding?.IsChecked == true) pages.Add(SeatMapPanel);

            if (pages.Count == 0)
            {
                TextBlock tb = new TextBlock
                {
                    Text = "Keine Panels ausgewählt",
                    FontSize = 28,
                    Foreground = System.Windows.Media.Brushes.Cyan,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pages.Add(new Border { Child = tb });
            }

            foreach (var p in new[] { FlightDataPanel, MapPanel, PassengerPanel, SeatMapPanel })
                if (p != null) p.Visibility = Visibility.Collapsed;

            currentPageIndex = (currentPageIndex + 1) % pages.Count;
            pages[currentPageIndex].Visibility = Visibility.Visible;

            if (pages[currentPageIndex] == MapPanel && FlightMapWebView?.CoreWebView2 != null)
            {
                _ = FlightMapWebView.CoreWebView2.ExecuteScriptAsync(
                    $"updatePlane({CurrentLat}, {CurrentLon});");
            }
        }

        // ===============================
        // Einstellungen übernehmen
        // ===============================
        private void BtnApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(TxtRotationInterval.Text, out double interval))
                {
                    rotationTimer.Interval = TimeSpan.FromSeconds(interval);
                    ConfigService.Current.Paths.Overlay.RotationIntervalSeconds = (int)interval;
                }

                if (int.TryParse(TxtWebServerPort.Text, out int port))
                {
                    ConfigService.Current.Paths.Overlay.ServerPort = port;
                }

                webServer?.Stop();
                webServer = new WebServer();
                webServer.Start();

                WebServerUrlTextBlock.Text =
                    $"URL: http://localhost:{TxtWebServerPort.Text}/FlightOverlay.html";

                OverlayLog.Text += "Einstellungen übernommen\n";
                ConfigService.Save();
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim Übernehmen der Einstellungen: {ex.Message}\n";
            }
        }

        // ===============================
        // SeatMap laden
        // ===============================
        private void LoadSeatMap(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                using var ms = new MemoryStream(File.ReadAllBytes(path));
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                SeatMapImage.Source = img;
            }
            catch
            {
                OverlayLog.Text += $"SeatMap-Bild gerade gesperrt: {path}\n";
            }
        }

        private void RefreshSeatMap()
        {
            if (File.Exists(seatMapPath))
                LoadSeatMap(seatMapPath);
        }

        // ===============================
        // Map initialisieren
        // ===============================
        private async Task InitMapAsync()
        {
            try
            {
                string htmlPath = Path.Combine(appRoot, "FlightMap.html");
                if (!File.Exists(htmlPath))
                {
                    OverlayLog.Text += $"FlightMap.html nicht gefunden: {htmlPath}\n";
                    return;
                }

                await FlightMapWebView.EnsureCoreWebView2Async();
                string fileUri = $"file:///{htmlPath.Replace("\\", "/")}";
                FlightMapWebView.CoreWebView2.Navigate(fileUri);

                FlightMapWebView.NavigationCompleted += async (s, e) =>
                {
                    await FlightMapWebView.CoreWebView2.ExecuteScriptAsync(
                        $"updatePlane({CurrentLat}, {CurrentLon});");

                    OverlayLog.Text += "FlightMap geladen und Marker gesetzt.\n";
                };
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim Laden der Map: {ex.Message}\n";
            }
        }
    }
}
