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
        private string seatMapPath => Path.Combine(appRoot, "boarding_render.png");


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
        public static bool ShowFlightDataStatic { get; private set; }
        public static bool ShowMapStatic { get; private set; }
        public static bool ShowPassengerStatic { get; private set; }
        public static bool ShowBoardingStatic { get; private set; }
        public static bool ShowCopyDataStatic { get; private set; } = true; // immer true

        public static bool ShowManualAircraftStatic { get; private set; } = false;
        public static bool ShowManualDepStatic { get; private set; } = false;
        public static bool ShowManualArrStatic { get; private set; } = false;

        public static bool ShowCopyAltitudeStatic { get; private set; } = true;
        public static bool ShowCopySpeedStatic { get; private set; } = true;
        public static bool ShowCopyHeadingStatic { get; private set; } = true;
        public static bool ShowCopyPositionStatic { get; private set; } = true;
        public static bool ShowCopyVSpeedStatic { get; private set; } = true;

        public static string AircraftTypeValueStatic { get; private set; } = "B737";
        public static string DepValueStatic { get; private set; } = "EDDF";
        public static string ArrValueStatic { get; private set; } = "EDDM";

        // ===============================
        // Konstruktor
        // ===============================
        public FlightDataOverlayControl()
        {
            InitializeComponent();

            // Preview Zeilen dynamisch ein-/ausblenden
            ChkManualAircraft.Checked += (s, e) => PreviewAircraftLine.Visibility = Visibility.Visible;
            ChkManualAircraft.Unchecked += (s, e) => PreviewAircraftLine.Visibility = Visibility.Collapsed;

            ChkManualDep.Checked += (s, e) => PreviewDepLine.Visibility = Visibility.Visible;
            ChkManualDep.Unchecked += (s, e) => PreviewDepLine.Visibility = Visibility.Collapsed;

            ChkManualArr.Checked += (s, e) => PreviewArrLine.Visibility = Visibility.Visible;
            ChkManualArr.Unchecked += (s, e) => PreviewArrLine.Visibility = Visibility.Collapsed;

            // Initiale Sichtbarkeit
            PreviewAircraftLine.Visibility = ChkManualAircraft.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PreviewDepLine.Visibility = ChkManualDep.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PreviewArrLine.Visibility = ChkManualArr.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;


            // --- CopyDataPanel permanent sichtbar ---
            CopyDataPanel.Visibility = Visibility.Visible;

            // --- Events für einzelne Copy-Zeilen ---
            ChkCopyAltitude.Checked += (s, e) => altitudeCopyLine.Visibility = Visibility.Visible;
            ChkCopyAltitude.Unchecked += (s, e) => altitudeCopyLine.Visibility = Visibility.Collapsed;

            ChkCopySpeed.Checked += (s, e) => speedCopyLine.Visibility = Visibility.Visible;
            ChkCopySpeed.Unchecked += (s, e) => speedCopyLine.Visibility = Visibility.Collapsed;

            ChkCopyHeading.Checked += (s, e) => headingCopyLine.Visibility = Visibility.Visible;
            ChkCopyHeading.Unchecked += (s, e) => headingCopyLine.Visibility = Visibility.Collapsed;

            ChkCopyPosition.Checked += (s, e) => coordinatesCopyLine.Visibility = Visibility.Visible;
            ChkCopyPosition.Unchecked += (s, e) => coordinatesCopyLine.Visibility = Visibility.Collapsed;

            ChkCopyVSpeed.Checked += (s, e) => vSpeedCopyLine.Visibility = Visibility.Visible;
            ChkCopyVSpeed.Unchecked += (s, e) => vSpeedCopyLine.Visibility = Visibility.Collapsed;

            // --- Initiale Sichtbarkeit setzen ---
            altitudeCopyLine.Visibility = ChkCopyAltitude.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            speedCopyLine.Visibility = ChkCopySpeed.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            headingCopyLine.Visibility = ChkCopyHeading.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            coordinatesCopyLine.Visibility = ChkCopyPosition.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            vSpeedCopyLine.Visibility = ChkCopyVSpeed.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            DataContext = this;

       
            // --- Standard Rotationsintervall 10 Sekunden ---
            int defaultRotation = ConfigService.Current?.Paths?.Overlay?.RotationIntervalSeconds ?? 10;
            TxtRotationInterval.Text = defaultRotation.ToString();
            rotationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(defaultRotation)
            };
            rotationTimer.Tick += (s, e) => RotatePages();

            // --- Unterbaum direkt sichtbar, wenn Häkchen gesetzt ---
            FlightDataMenu.Visibility = (ChkFlightData.IsChecked == true)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // --- Event-Handler für Flugdaten-Untermenü ---
            ChkFlightData.Checked += (s, e) => FlightDataMenu.Visibility = Visibility.Visible;
            ChkFlightData.Unchecked += (s, e) => FlightDataMenu.Visibility = Visibility.Collapsed;

            // --- Passagiere initial in Queue ---
            foreach (var p in PassengerStore.Passengers)
                passengerQueue.Enqueue(p);

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
            // ===============================
            // Statische Flags für WebServer
            // ===============================
            ShowFlightDataStatic = ChkFlightData?.IsChecked == true;
            ShowMapStatic = ChkMap?.IsChecked == true;
            ShowPassengerStatic = ChkPassenger?.IsChecked == true;
            ShowBoardingStatic = ChkBoarding?.IsChecked == true;
            ShowCopyDataStatic = true; // permanent sichtbar

            // Einzelne Kopie-Zeilen Flags (für WebServer)
            ShowCopyAltitudeStatic = ChkCopyAltitude?.IsChecked == true;
            ShowCopySpeedStatic = ChkCopySpeed?.IsChecked == true;
            ShowCopyHeadingStatic = ChkCopyHeading?.IsChecked == true;
            ShowCopyPositionStatic = ChkCopyPosition?.IsChecked == true;
            ShowCopyVSpeedStatic = ChkCopyVSpeed?.IsChecked == true;

            try
            {
                // ===============================
                // Live Flugdaten abrufen
                // ===============================
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
                // Manuelle Eingaben
                // ===============================
                ShowManualAircraftStatic = ChkManualAircraft?.IsChecked == true;
                ShowManualDepStatic = ChkManualDep?.IsChecked == true;
                ShowManualArrStatic = ChkManualArr?.IsChecked == true;

                AircraftTypeValueStatic = TxtAircraftType?.Text ?? "A320";
                DepValueStatic = TxtDep?.Text ?? "EDDF";
                ArrValueStatic = TxtArr?.Text ?? "EDDM";

                // ===============================
                // Vorschau in den neuen TextBoxen setzen
                // ===============================
                PreviewAircraft.Text = (ChkManualAircraft.IsChecked == true) ? TxtAircraftType.Text : "";
                PreviewDep.Text = (ChkManualDep.IsChecked == true) ? TxtDep.Text : "";
                PreviewArr.Text = (ChkManualArr.IsChecked == true) ? TxtArr.Text : "";

                // ===============================
                // Kopierte Flugdaten setzen (Texboxen)
                // ===============================
                TxtCopyAltitude.Text = CurrentAltitude.ToString("F0");
                TxtCopySpeed.Text = CurrentSpeed.ToString("F0");
                TxtCopyHeading.Text = CurrentHeading.ToString("F0");
                TxtCopyPosition.Text = $"{CurrentLat:F6}, {CurrentLon:F6}";
                TxtCopyVSpeed.Text = VSpeed.ToString("F0");

                // ===============================
                // Map aktualisieren
                // ===============================
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
            RefreshSeatMap();
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

        // ===============================
        // SeatMap laden (optimiert)
        // ===============================
        private DateTime lastSeatMapWriteTime = DateTime.MinValue;

        private void RefreshSeatMap()
        {
            if (!File.Exists(seatMapPath)) return;

            try
            {
                // Prüfen, ob die Datei seit dem letzten Laden geändert wurde
                DateTime writeTime = File.GetLastWriteTimeUtc(seatMapPath);
                if (writeTime == lastSeatMapWriteTime) return; // nichts zu tun

                lastSeatMapWriteTime = writeTime;

                using var ms = new MemoryStream(File.ReadAllBytes(seatMapPath));
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
                OverlayLog.Text += $"SeatMap-Bild gerade gesperrt: {seatMapPath}\n";
            }
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
