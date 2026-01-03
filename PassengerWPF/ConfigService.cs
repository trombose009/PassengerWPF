using System;
using System.IO;
using System.Text.Json;

namespace PassengerWPF
{
    public class Config
    {
        public PathsSection Paths { get; set; } = new();
        public CsvSection Csv { get; set; } = new();
        public OverlaySection Overlay { get; set; } = new OverlaySection();
    }

    public class OverlaySection
    {
        // Panels
        public bool ShowFlightData { get; set; } = true;
        public bool ShowMap { get; set; } = true;
        public bool ShowPassengerList { get; set; } = true;
        public bool ShowBoarding { get; set; } = true;

        // Rotation
        public int RotationIntervalSeconds { get; set; } = 10;

        // Flugdaten-Details
        public bool ShowAltitude { get; set; } = true;
        public bool ShowSpeed { get; set; } = true;
        public bool ShowHeading { get; set; } = true;
        public bool ShowPosition { get; set; } = true;
        public bool ShowVSpeed { get; set; } = true;

        // Flight Info
        public string Departure { get; set; } = "";
        public string Arrival { get; set; } = "";

        // Webserver
        public int ServerPort { get; set; } = 8080;

        // SimBridge
        public string SimBridgeIp { get; set; } = "localhost";
        public int SimBridgePort { get; set; } = 8380;
    }

    public class PathsSection
    {
        public string Avatars { get; set; } = "";
        public string Orders { get; set; } = "";
        public string Stewardess { get; set; } = "";
        public string Cabin { get; set; } = "";
        public string BGImage { get; set; } = "";
        public string BoardingSound { get; set; } = "";
        public string CateringMusic { get; set; } = "";
        public string FrequentFlyerBg { get; set; } = "";

        public OverlaySection Overlay { get; set; } = new OverlaySection();
    }

    public class CsvSection
    {
        public string PassengerData { get; set; } = "";
        public string AvatarDb { get; set; } = "";
        public string ActualFlight { get; set; } = "";
        public string Orders { get; set; } = "";
        public string BoardingCount { get; set; } = "";
        public string CurrentFlightId { get; set; } = "";
    }

    public static class ConfigService
    {
        public static Config Current { get; private set; } = new();

        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                Current = new Config();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Config>(json) ?? new Config();

                loaded.Paths ??= new PathsSection();
                loaded.Csv ??= new CsvSection();
                loaded.Overlay ??= new OverlaySection();
                loaded.Paths.Overlay ??= loaded.Overlay;

                Current = loaded;

                Save();
            }
            catch (Exception ex)
            {
                Current = new Config();
                Save();
                Console.WriteLine($"Fehler beim Laden der config.json: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Current,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern der config.json: {ex.Message}");
            }
        }
    }
}
