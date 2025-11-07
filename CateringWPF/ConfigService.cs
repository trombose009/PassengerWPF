using System.IO;
using System.Text.Json;

namespace CateringWPF
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
    }

    public class CsvSection
    {
        public string Catering { get; set; } = "";
        public string AvatarDb { get; set; } = "";
        public string ActualFlight { get; set; } = "";
        public string Orders { get; set; } = "";
    }

    public static class ConfigService
    {
        public static Config Current { get; private set; } = new();
        private static readonly string ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

        public static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                Current = JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
        }

        public static void Save()
        {
            string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
