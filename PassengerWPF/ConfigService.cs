using System;
using System.IO;
using System.Text.Json;

namespace PassengerWPF
{
    // -------------------------
    // Konfigurationsklassen
    // -------------------------
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
        public string Orders { get; set; } = "";
        public string BoardingCount { get; set; } = "";
        public string CurrentFlightId { get; set; } = "";
    }

    // -------------------------
    // Hilfsklasse für relative Pfade
    // -------------------------
    public static class PathHelpers
    {
        public static string MakeRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
            string exeRoot = AppDomain.CurrentDomain.BaseDirectory;
            Uri pathUri = new Uri(absolutePath);
            Uri rootUri = new Uri(exeRoot);
            Uri relativeUri = rootUri.MakeRelativeUri(pathUri);
            return relativeUri.ToString().Replace('/', '\\');
        }

        public static string MakeAbsolute(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath));
        }
    }

    // -------------------------
    // ConfigService
    // -------------------------
    public static class ConfigService
    {
        public static Config Current { get; private set; } = new();

        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        // -------------------------
        // Config laden
        // -------------------------
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

                // --- Relative Pfade beim Laden in absolute auflösen ---
                ResolvePathsToAbsolute();

                Save();
            }
            catch (Exception ex)
            {
                Current = new Config();
                Save();
                Console.WriteLine($"Fehler beim Laden der config.json: {ex.Message}");
            }
        }

        // -------------------------
        // Config speichern
        // -------------------------
        public static void Save()
        {
            try
            {
                // --- Pfade vor dem Speichern in relative konvertieren ---
                ConvertPathsToRelative();

                string json = JsonSerializer.Serialize(Current,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(ConfigPath, json);

                // --- Danach wieder absolute Pfade für App behalten ---
                ResolvePathsToAbsolute();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Speichern der config.json: {ex.Message}");
            }
        }

        // -------------------------
        // Hilfsmethoden
        // -------------------------
        private static void ConvertPathsToRelative()
        {
            Current.Paths.Avatars = PathHelpers.MakeRelative(Current.Paths.Avatars);
            Current.Paths.Orders = PathHelpers.MakeRelative(Current.Paths.Orders);
            Current.Paths.Stewardess = PathHelpers.MakeRelative(Current.Paths.Stewardess);
            Current.Paths.Cabin = PathHelpers.MakeRelative(Current.Paths.Cabin);
            Current.Paths.BGImage = PathHelpers.MakeRelative(Current.Paths.BGImage);
            Current.Paths.BoardingSound = PathHelpers.MakeRelative(Current.Paths.BoardingSound);
            Current.Paths.CateringMusic = PathHelpers.MakeRelative(Current.Paths.CateringMusic);
            Current.Paths.FrequentFlyerBg = PathHelpers.MakeRelative(Current.Paths.FrequentFlyerBg);

            Current.Csv.PassengerData = PathHelpers.MakeRelative(Current.Csv.PassengerData);
            Current.Csv.AvatarDb = PathHelpers.MakeRelative(Current.Csv.AvatarDb);
            Current.Csv.Orders = PathHelpers.MakeRelative(Current.Csv.Orders);
            Current.Csv.BoardingCount = PathHelpers.MakeRelative(Current.Csv.BoardingCount);
        }

        private static void ResolvePathsToAbsolute()
        {
            Current.Paths.Avatars = PathHelpers.MakeAbsolute(Current.Paths.Avatars);
            Current.Paths.Orders = PathHelpers.MakeAbsolute(Current.Paths.Orders);
            Current.Paths.Stewardess = PathHelpers.MakeAbsolute(Current.Paths.Stewardess);
            Current.Paths.Cabin = PathHelpers.MakeAbsolute(Current.Paths.Cabin);
            Current.Paths.BGImage = PathHelpers.MakeAbsolute(Current.Paths.BGImage);
            Current.Paths.BoardingSound = PathHelpers.MakeAbsolute(Current.Paths.BoardingSound);
            Current.Paths.CateringMusic = PathHelpers.MakeAbsolute(Current.Paths.CateringMusic);
            Current.Paths.FrequentFlyerBg = PathHelpers.MakeAbsolute(Current.Paths.FrequentFlyerBg);

            Current.Csv.PassengerData = PathHelpers.MakeAbsolute(Current.Csv.PassengerData);
            Current.Csv.AvatarDb = PathHelpers.MakeAbsolute(Current.Csv.AvatarDb);
            Current.Csv.Orders = PathHelpers.MakeAbsolute(Current.Csv.Orders);
            Current.Csv.BoardingCount = PathHelpers.MakeAbsolute(Current.Csv.BoardingCount);
        }
    }
}
