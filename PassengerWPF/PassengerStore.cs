using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PassengerWPF
{
    public static class PassengerStore
    {
        private static readonly string SavePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boarding_state.json");

        public static ObservableCollection<PassengerViewModel> Passengers { get; }
            = new();

        public static event Action<string> SeatMapUpdated;

        // =========================
        //  ZENTRALE REGEL
        // =========================
        public static bool ContainsPassenger(string name)
        {
            return Passengers.Any(p => p.Name == name);
        }

        public static void AddPassenger(PassengerViewModel passenger)
        {
            if (ContainsPassenger(passenger.Name))
                return;

            Passengers.Add(passenger);
            SaveState();
        }

        public static void Clear()
        {
            Passengers.Clear();
            SaveState();
        }

        // =========================
        //  PERSISTENZ
        // =========================
        public static void LoadState()
        {
            if (!File.Exists(SavePath))
                return;

            try
            {
                var data = JsonSerializer.Deserialize<PassengerViewModel[]>(
                    File.ReadAllText(SavePath));

                Passengers.Clear();
                foreach (var p in data)
                    Passengers.Add(p);
            }
            catch
            {
                // bewusst still
            }
        }

        private static void SaveState()
        {
            try
            {
                File.WriteAllText(
                    SavePath,
                    JsonSerializer.Serialize(Passengers));
            }
            catch
            {
                // bewusst still
            }
        }

        public static void NotifySeatMapUpdated(string path)
        {
            SeatMapUpdated?.Invoke(path);
        }
    }
}
