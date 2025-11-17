using System;
using System.IO;
using System.Text.Json;

namespace PassengerWPF
{
    public class Config
    {
        public PathsSection Paths { get; set; } = new();
        public CsvSection Csv { get; set; } = new();
    }

    public class PathsSection
    {
        public string Avatars { get; set; } = "";
        public string Orders { get; set; } = "";
        public string Stewardess { get; set; } = "";
        public string Cabin { get; set; } = "";
        public string BGImage { get; set; } = "";
        public string BoardingSound { get; set; }
        public string CabinMusic { get; set; }
    }

    public class CsvSection
    {
        public string PassengerData { get; set; } = "";
        public string AvatarDb { get; set; } = "";
        public string ActualFlight { get; set; } = "";
        public string Orders { get; set; } = "";
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
                // Wenn config.json fehlt, lege Standarddatei an
                Current = new Config();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Config>(json) ?? new Config();

                // Sicherstellen, dass alle Unterobjekte existieren
                loaded.Paths ??= new PathsSection();
                loaded.Csv ??= new CsvSection();

                Current = loaded;

                // Optional: Datei neu schreiben, um fehlende Felder sichtbar zu machen
                Save();
            }
            catch (Exception ex)
            {
                // Bei Fehlern immer Default anlegen
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
