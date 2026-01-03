using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private DispatcherTimer passengerTimer; // Timer für stufenweises Einblenden
        private int currentPageIndex = -1;

        private SimConnectClient simconnect;
        private WebServer webServer;

        // Live-Daten
        public static double CurrentAltitude { get; private set; }
        public static double CurrentSpeed { get; private set; }
        public static double CurrentHeading { get; private set; }
        public static double CurrentLat { get; private set; }
        public static double CurrentLon { get; private set; }
        public static double VSpeed { get; private set; }

        // Dynamische Passagierliste für das Overlay
        public ObservableCollection<PassengerViewModel> OverlayPassengers { get; } = new ObservableCollection<PassengerViewModel>();

        // Warteschlange für stufenweises Einblenden
        private Queue<PassengerViewModel> passengerQueue = new Queue<PassengerViewModel>();

        public FlightDataOverlayControl()
        {
            InitializeComponent();
            DataContext = this;

            // Initiale Passagiere in die Queue packen
            foreach (var p in PassengerStore.Passengers)
                passengerQueue.Enqueue(p);

            // Neue Passagiere automatisch zur Queue hinzufügen
            PassengerStore.Passengers.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (PassengerViewModel p in e.NewItems)
                        passengerQueue.Enqueue(p);
                }
            };

            // Timer für stufenweises Hinzufügen von Passagieren
            passengerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            passengerTimer.Tick += (s, e) =>
            {
                if (passengerQueue.Count == 0) return;
                OverlayPassengers.Add(passengerQueue.Dequeue());
            };

            // Timer für Flugdaten
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            updateTimer.Tick += async (s, e) => await UpdateFlightDataAsync();

            // Timer für Overlay-Rotation
            rotationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ConfigService.Current?.Paths?.Overlay?.RotationIntervalSeconds ?? 20)
            };
            rotationTimer.Tick += (s, e) => RotatePages();

            Loaded += FlightDataOverlayControl_Loaded;
        }

        private void FlightDataOverlayControl_Loaded(object sender, RoutedEventArgs e)
        {
            StartService();
        }

        public async void StartService()
        {
            try
            {
                await InitSimConnectAsync();

                updateTimer.Start();
                rotationTimer.Start();
                passengerTimer.Start();

                if (webServer != null) webServer.Stop();
                webServer = new WebServer();
                webServer.Start();

                WebServerUrlTextBlock.Text = $"URL: http://localhost:{ConfigService.Current?.Paths?.Overlay?.ServerPort ?? 8080}/FlightOverlay.html";
                OverlayLog.Text += "Service gestartet\n";

                RefreshSeatMap();
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
            if (simconnect == null || !simconnect.IsConnected) return;

            try
            {
                CurrentAltitude = await simconnect.SimVars.GetAsync<double>("PLANE ALTITUDE", "feet");
                CurrentSpeed = await simconnect.SimVars.GetAsync<double>("AIRSPEED INDICATED", "knots");
                CurrentHeading = await simconnect.SimVars.GetAsync<double>("PLANE HEADING DEGREES TRUE", "degrees");
                CurrentLat = await simconnect.SimVars.GetAsync<double>("PLANE LATITUDE", "degrees");
                CurrentLon = await simconnect.SimVars.GetAsync<double>("PLANE LONGITUDE", "degrees");
                VSpeed = await simconnect.SimVars.GetAsync<double>("VERTICAL SPEED", "feet per minute");

                PreviewTextBlock.Text =
                    $"Altitude: {CurrentAltitude:F0} ft\n" +
                    $"Speed: {CurrentSpeed:F0} kt\n" +
                    $"Heading: {CurrentHeading:F0}°\n" +
                    $"Position: {CurrentLat:F4}, {CurrentLon:F4}\n" +
                    $"VSpeed: {VSpeed:F0} ft/min";
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim Abrufen der Flugdaten: {ex.Message}\n";
            }
        }

        private void RotatePages()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RotatePages);
                return;
            }

            var pages = new List<Border>();
            if (ChkFlightData?.IsChecked == true && FlightDataPanel != null) pages.Add(FlightDataPanel);
            if (ChkMap?.IsChecked == true && MapPanel != null) pages.Add(MapPanel);
            if (ChkPassenger?.IsChecked == true && PassengerPanel != null) pages.Add(PassengerPanel);
            if (ChkBoarding?.IsChecked == true && SeatMapPanel != null) pages.Add(SeatMapPanel);

            if (pages.Count == 0) return;

            foreach (var panel in new[] { FlightDataPanel, MapPanel, PassengerPanel, SeatMapPanel })
                if (panel != null) panel.Visibility = Visibility.Collapsed;

            currentPageIndex = (currentPageIndex + 1) % pages.Count;
            pages[currentPageIndex].Visibility = Visibility.Visible;

            if (pages[currentPageIndex] == SeatMapPanel)
                RefreshSeatMap();
        }

        private void BtnApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(TxtRotationInterval.Text, out double interval))
                {
                    if (rotationTimer != null)
                        rotationTimer.Interval = TimeSpan.FromSeconds(interval);

                    if (ConfigService.Current?.Paths?.Overlay != null)
                        ConfigService.Current.Paths.Overlay.RotationIntervalSeconds = (int)interval;
                }

                if (int.TryParse(TxtWebServerPort.Text, out int port))
                {
                    if (ConfigService.Current?.Paths?.Overlay != null)
                        ConfigService.Current.Paths.Overlay.ServerPort = port;
                }

                if (webServer != null)
                {
                    webServer.Stop();
                    webServer = new WebServer();
                    webServer.Start();
                }

                WebServerUrlTextBlock.Text = $"URL: http://localhost:{TxtWebServerPort.Text}/FlightOverlay.html";
                OverlayLog.Text += $"Einstellungen übernommen: Intervall {interval}s, Webserver Port {TxtWebServerPort.Text}\n";

                ConfigService.Save();
            }
            catch (Exception ex)
            {
                OverlayLog.Text += $"Fehler beim Übernehmen der Einstellungen: {ex.Message}\n";
            }
        }

        private void LoadSeatMap(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    using var ms = new MemoryStream(bytes);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                    img.Freeze();
                    SeatMapImage.Source = img;
                });
            }
            catch
            {
                OverlayLog.Text += $"Boarding-Bild gerade gesperrt: {path}\n";
            }
        }

        private void RefreshSeatMap()
        {
            string path = @"E:\FS-Addons\msfs-eigene\BoardingSystem\boarding\boarding_render.png";
            if (!File.Exists(path)) return;
            LoadSeatMap(path);
        }
    }
}
