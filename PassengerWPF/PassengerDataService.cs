using CsvHelper;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;

namespace PassengerWPF
{
    public static class PassengerDataService
    {
        public static List<Passenger> LoadPassengers(string csvPath)
        {
            var passengers = new List<Passenger>();

            if (!File.Exists(csvPath))
                return passengers;

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            passengers = new List<Passenger>(csv.GetRecords<Passenger>());

            return passengers;
        }
    }
}
