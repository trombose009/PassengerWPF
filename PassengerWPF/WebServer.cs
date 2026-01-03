using System;
using System.IO;
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
                Console.WriteLine($"Webserver gestartet: {prefix}");
                Task.Run(() => ListenLoop());
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Fehler beim Starten des Webservers: {ex.Message}");
            }
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
                Console.WriteLine("Webserver gestoppt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Stoppen des Webservers: {ex.Message}");
            }
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
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Webserver-Fehler: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            string url = context.Request.Url.AbsolutePath.ToLower();

            if (url == "/" || url.EndsWith("flightoverlay.html"))
            {
                // HTML im gleichen Verzeichnis wie boarding_render.png suchen
                string htmlPath = Path.Combine(
                    Path.GetDirectoryName(ConfigService.Current.Paths.BGImage) ?? "",
                    "FlightOverlay.html"
                );

                string responseString;
                if (File.Exists(htmlPath))
                    responseString = File.ReadAllText(htmlPath);
                else
                    responseString = "<html><body><h1>FlightOverlay.html fehlt!</h1></body></html>";

                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "text/html; charset=UTF-8";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            else if (url.EndsWith("/data"))
            {
                var overlay = ConfigService.Current.Paths.Overlay;

                var data = new
                {
                    ShowFlightData = overlay.ShowFlightData,
                    ShowMap = overlay.ShowMap,
                    ShowPassenger = overlay.ShowPassengerList,
                    ShowBoarding = overlay.ShowBoarding,
                    RotationInterval = overlay.RotationIntervalSeconds,

                    altitude = FlightDataOverlayControl.CurrentAltitude,
                    speed = FlightDataOverlayControl.CurrentSpeed,
                    heading = FlightDataOverlayControl.CurrentHeading,
                    latitude = FlightDataOverlayControl.CurrentLat,
                    longitude = FlightDataOverlayControl.CurrentLon,
                    vSpeed = FlightDataOverlayControl.VSpeed,
                    dep = overlay.Departure,
                    arr = overlay.Arrival,
                    aircraftType = "B737",
                    fps = 0
                };

                string json = JsonSerializer.Serialize(data);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "application/json; charset=UTF-8";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
    }
}
