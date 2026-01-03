using System;
using System.Collections.ObjectModel;

namespace PassengerWPF
{
    public static class PassengerStore
    {
        // Dynamische Passagierliste
        public static ObservableCollection<PassengerViewModel> Passengers { get; } = new();

        // Event für neue SeatMap-Screenshots
        public static event Action<string> SeatMapUpdated;

        // Methode zum Hinzufügen eines Passagiers
        public static void AddPassenger(PassengerViewModel passenger)
        {
            if (!Passengers.Contains(passenger))
                Passengers.Add(passenger);
        }

        // Methode zum Feuern des SeatMap-Events
        public static void NotifySeatMapUpdated(string path)
        {
            SeatMapUpdated?.Invoke(path);
        }
    }
}
