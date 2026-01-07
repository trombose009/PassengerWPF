using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PassengerWPF
{
    public class WebServer
    {
        private HttpListener listener;
        private bool isRunning = false;

        public WebServer() { }

        public void Start()
        {
            if (isRunning) return;

            int port = ConfigService.Current.Paths.Overlay.ServerPort;
            string prefix = $"http://localhost:{port}/";

            listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                isRunning = true;
                Task.Run(() => ListenLoop());
            }
            catch { }
        }

        public void Stop()
        {
            if (!isRunning) return;
            try
            {
                isRunning = false;
                listener.Stop();
                listener.Close();
                listener = null;
            }
            catch { }
        }

        private async Task ListenLoop()
        {
            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    await HandleRequest(context);
                }
                catch { }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            string url = context.Request.Url.AbsolutePath.ToLower().TrimStart('/');
            string appRoot = AppDomain.CurrentDomain.BaseDirectory;

            // HTML
            if (string.IsNullOrEmpty(url) || url == "flightoverlay.html")
            {
                string htmlPath = Path.Combine(appRoot, "FlightOverlay.html");
                string responseString = File.Exists(htmlPath)
                    ? File.ReadAllText(htmlPath)
                    : "<html><body><h1>FlightOverlay.html fehlt!</h1></body></html>";

                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "text/html; charset=UTF-8";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                return;
            }

            // JSON Daten
            if (url == "data")
            {
                var overlay = ConfigService.Current.Paths.Overlay;
                var passengerNames = FlightDataOverlayControl.OverlayPassengers.Select(p => p.Name).ToArray();

                var data = new
                {
                    // Panel Flags
                    ShowFlightData = FlightDataOverlayControl.ShowFlightDataStatic,
                    ShowMap = FlightDataOverlayControl.ShowMapStatic,
                    ShowPassenger = FlightDataOverlayControl.ShowPassengerStatic,
                    ShowBoarding = FlightDataOverlayControl.ShowBoardingStatic,
                    RotationInterval = overlay.RotationIntervalSeconds,

                    // Automatische Flugdaten, nur wenn Flag true
                    altitude = overlay.ShowAltitude ? FlightDataOverlayControl.CurrentAltitude : (double?)null,
                    speed = overlay.ShowSpeed ? FlightDataOverlayControl.CurrentSpeed : (double?)null,
                    heading = overlay.ShowHeading ? FlightDataOverlayControl.CurrentHeading : (double?)null,
                    latitude = overlay.ShowPosition ? FlightDataOverlayControl.CurrentLat : (double?)null,
                    longitude = overlay.ShowPosition ? FlightDataOverlayControl.CurrentLon : (double?)null,
                    vSpeed = overlay.ShowVSpeed ? FlightDataOverlayControl.VSpeed : (double?)null,

                    ShowAltitude = overlay.ShowAltitude,
                    ShowSpeed = overlay.ShowSpeed,
                    ShowHeading = overlay.ShowHeading,
                    ShowVSpeed = overlay.ShowVSpeed,
                    ShowPosition = overlay.ShowPosition,

                    // Manuelle Werte nur, wenn Flag gesetzt
                    aircraftType = FlightDataOverlayControl.ShowManualAircraftStatic ? FlightDataOverlayControl.AircraftTypeValueStatic : "",
                    dep = FlightDataOverlayControl.ShowManualDepStatic ? FlightDataOverlayControl.DepValueStatic : "",
                    arr = FlightDataOverlayControl.ShowManualArrStatic ? FlightDataOverlayControl.ArrValueStatic : "",

                    // Passagiere
                    passengers = passengerNames
                };

                string json = JsonSerializer.Serialize(data);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json; charset=UTF-8";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                return;
            }

            // Bilder
            if (url.EndsWith(".png") || url.EndsWith(".jpg") || url.EndsWith(".jpeg"))
            {
                string imgPath = Path.Combine(appRoot, url.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(imgPath))
                    imgPath = Path.Combine(appRoot, "stuff", Path.GetFileName(url));

                if (File.Exists(imgPath))
                {
                    byte[] buffer = File.ReadAllBytes(imgPath);
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.ContentType = "image/png";
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                    return;
                }
            }

            // 404
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }
}