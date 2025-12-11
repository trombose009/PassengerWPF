using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PassengerWPF
{
    public static class PassengerDataService
    {
        private static readonly Random random = new();

        public static List<Passenger> LoadPassengers(string csvPath, string avatarsPath, List<string> allSeats)
        {
            var passengers = new List<Passenger>();

            if (!File.Exists(csvPath))
                return passengers;

            // CSV sicher lesen (kein Lock!)
            using var reader = new StreamReader(
                new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            );

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };

            using var csv = new CsvReader(reader, config);
            passengers = csv.GetRecords<Passenger>().ToList();

            bool changed = false;

            // Sitzplätze sammeln
            var takenSeats = passengers
                .Where(p => !string.IsNullOrEmpty(p.Sitzplatz))
                .Select(p => p.Sitzplatz.ToUpper())
                .ToHashSet();

            // Passagiere bearbeiten
            foreach (var p in passengers)
            {
                // Sitzplatz vergeben
                if (string.IsNullOrEmpty(p.Sitzplatz))
                {
                    var freeSeats = allSeats.Except(takenSeats).ToList();
                    if (freeSeats.Count > 0)
                    {
                        var seat = freeSeats[random.Next(freeSeats.Count)];
                        p.Sitzplatz = seat.ToUpper();
                        takenSeats.Add(seat.ToUpper());
                        changed = true;
                    }
                }

                // Avatar-Fallback
                if (string.IsNullOrEmpty(p.AvatarFile))
                {
                    p.AvatarFile = Path.Combine(avatarsPath, "default.png");
                    changed = true;
                }

                // Bestellbilder
                p.Order1 ??= "placeholder.png";
                p.Order2 ??= "placeholder.png";
                p.Order3 ??= "placeholder.png";
                p.Order4 ??= "placeholder.png";
            }

            // Nur schreiben, wenn Änderungen vorgenommen wurden
            if (changed)
            {
                using var writer = new StreamWriter(
                    new FileStream(csvPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)
                );

                using var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
                csvWriter.WriteRecords(passengers);
            }

            return passengers;
        }
    }
}
