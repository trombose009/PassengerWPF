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
            bool changed = false; // Merkt sich, ob die CSV neu geschrieben werden muss

            if (!File.Exists(csvPath))
                return passengers;

            // CSV sicher lesen (kein Lock)
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

            // 🔹 Auto-Repair: ungültige Sitze erkennen
            foreach (var p in passengers)
            {
                if (!string.IsNullOrWhiteSpace(p.Sitzplatz))
                {
                    var seat = p.Sitzplatz.ToUpper().Trim();

                    if (!allSeats.Contains(seat))
                    {
                        // 🧹 Ungültigen Sitz entfernen → wird neu vergeben
                      //  File.AppendAllText(
                       //     "seat_autorepair.log",
                      //      $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {p.Name} | ungültiger Sitz repariert: {seat}\n"
                  //      );

                        p.Sitzplatz = null;
                        changed = true;
                    }
                    else
                    {
                        // Normalisieren
                        p.Sitzplatz = seat;
                    }
                }
            }

            // 🔹 Alle aktuell belegten Sitze sammeln (nach Repair!)
            var takenSeats = passengers
                .Where(p => !string.IsNullOrEmpty(p.Sitzplatz))
                .Select(p => p.Sitzplatz.ToUpper())
                .ToHashSet();

            // 🔹 Passagiere bearbeiten
            foreach (var p in passengers)
            {
                // Sitzplatz vergeben, wenn leer
                if (string.IsNullOrEmpty(p.Sitzplatz))
                {
                    var freeSeats = allSeats.Except(takenSeats).ToList();
                    if (freeSeats.Count > 0)
                    {
                        var seat = freeSeats[random.Next(freeSeats.Count)];
                        p.Sitzplatz = seat.ToUpper();
                        takenSeats.Add(p.Sitzplatz);
                        changed = true;

                        // Optional Log
                        File.AppendAllText(
                            "seat_autorepair.log",
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {p.Name} | Sitz automatisch vergeben: {p.Sitzplatz}\n"
                        );
                    }
                    else
                    {
                        // Kein freier Sitz mehr vorhanden
                        File.AppendAllText(
                            "seat_autorepair.log",
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {p.Name} | Kein freier Sitz verfügbar!\n"
                        );
                    }
                }

                // Avatar-Fallback
                if (string.IsNullOrEmpty(p.AvatarFile))
                {
                    p.AvatarFile = Path.Combine(avatarsPath, "default.png");
                    changed = true;
                }

                // Bestellbilder-Fallback
                p.Order1 ??= "placeholder.png";
                p.Order2 ??= "placeholder.png";
                p.Order3 ??= "placeholder.png";
                p.Order4 ??= "placeholder.png";
            }

            // 🔹 CSV nur neu schreiben, wenn Änderungen erfolgt sind
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
