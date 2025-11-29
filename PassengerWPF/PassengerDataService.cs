using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PassengerWPF
{
    public static class PassengerDataService
    {
        public static List<Passenger> LoadPassengers(string csvPath, string avatarsPath)
        {
            var passengers = new List<Passenger>();

            if (!File.Exists(csvPath))
                return passengers;

            using var reader = new StreamReader(csvPath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,  // fehlende Header ignorieren
                MissingFieldFound = null // fehlende Spalten ignorieren
            };

            using var csv = new CsvReader(reader, config);
            passengers = new List<Passenger>(csv.GetRecords<Passenger>());

            // leere Avatare automatisch mit dummy.png auffüllen
            foreach (var p in passengers)
            {
                if (string.IsNullOrEmpty(p.AvatarFile))
                {
                    p.AvatarFile = Path.Combine(avatarsPath, "dummy.png");
                }
            }

            return passengers;
        }
    }
}
