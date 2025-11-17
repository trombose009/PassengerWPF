using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PassengerWPF
{
    public partial class AvatarMappingWindow : Window
    {
        private readonly string passengerDataPath;
        private readonly string avatarDbPath;
        private readonly string avatarDir;
        private readonly string ordersDir;

        private ObservableCollection<PassengerViewModel> passengers = new();

        public AvatarMappingWindow()
        {
            InitializeComponent();

            passengerDataPath = ConfigService.Current.Csv.PassengerData; // <-- Csv, nicht Paths
            avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            avatarDir = ConfigService.Current.Paths.Avatars;
            ordersDir = ConfigService.Current.Paths.Orders;

            LoadPassengers();
            DataGridPassengers.ItemsSource = passengers;
        }

        private void LoadPassengers()
        {
            passengers.Clear();

            // Avatar-DB laden (Name → Avatar-Dateiname)
            var avatarDb = new System.Collections.Generic.Dictionary<string, string>();
            if (File.Exists(avatarDbPath))
            {
                foreach (var line in File.ReadAllLines(avatarDbPath).Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                        avatarDb[parts[0].Trim('"')] = parts[1].Trim('"');
                }
            }

            if (!File.Exists(passengerDataPath)) return;

            var lines = File.ReadAllLines(passengerDataPath).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 7) continue;

                string name = parts[0].Trim('"');
                string seat = parts[1].Trim('"');
                string avatarFile = parts[2];

                if (avatarDb.ContainsKey(name))
                    avatarFile = avatarDb[name];

                if (string.IsNullOrEmpty(avatarFile) || !File.Exists(Path.Combine(avatarDir, avatarFile)))
                    avatarFile = "placeholder.png";

                var p = new PassengerViewModel
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatarFile,
                    Order1 = parts[3],
                    Order2 = parts[4],
                    Order3 = parts[5],
                    Order4 = parts[6]
                };

                // AvatarImagePath und OrderXImagePath sind jetzt durch ViewModel-Setter bereits gesetzt
                passengers.Add(p);
            }
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel passenger) return;

            var dlg = new OpenFileDialog
            {
                Title = "Avatar auswählen",
                Filter = "PNG Dateien|*.png;*.jpg;*.jpeg",
                InitialDirectory = avatarDir
            };

            if (dlg.ShowDialog() == true)
            {
                // wir speichern nur Dateiname in Avatar, aber setzen auch den ImagePath auf den absoluten Pfad
                passenger.Avatar = Path.GetFileName(dlg.FileName);
                passenger.AvatarImagePath = dlg.FileName;

                // Sofort persistieren in Avatar-DB (falls du das noch willst)
                SaveAvatarDb();
                // und PassengerData weil Avatar-Filename geändert wurde
                SavePassengerData();
            }
        }

        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel passenger) return;

            if (!int.TryParse(btn.Tag?.ToString(), out int orderIndex))
            {
                MessageBox.Show("Fehler: Ungültiger Order-Index.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = $"Order {orderIndex} auswählen",
                Filter = "PNG Dateien|*.png;*.jpg;*.jpeg",
                InitialDirectory = ordersDir
            };

            if (dlg.ShowDialog() == true)
            {
                string filename = Path.GetFileName(dlg.FileName);

                switch (orderIndex)
                {
                    case 1: passenger.Order1 = filename; passenger.Order1ImagePath = dlg.FileName; break;
                    case 2: passenger.Order2 = filename; passenger.Order2ImagePath = dlg.FileName; break;
                    case 3: passenger.Order3 = filename; passenger.Order3ImagePath = dlg.FileName; break;
                    case 4: passenger.Order4 = filename; passenger.Order4ImagePath = dlg.FileName; break;
                }

                SavePassengerData();
            }
        }

        private void SavePassengerData()
        {
            try
            {
                using var writer = new StreamWriter(passengerDataPath);
                writer.WriteLine("Name,Sitzplatz,Avatar,Order1,Order2,Order3,Order4");

                foreach (var p in passengers)
                {
                    writer.WriteLine($"{p.Name},{p.Sitzplatz},{p.Avatar},{p.Order1},{p.Order2},{p.Order3},{p.Order4}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Speichern der PassengerData: " + ex.Message);
            }
        }

        private void SaveAvatarDb()
        {
            try
            {
                using var writer = new StreamWriter(avatarDbPath);
                writer.WriteLine("Name,Avatar");

                foreach (var p in passengers)
                {
                    writer.WriteLine($"{p.Name},{p.Avatar}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Speichern der Avatar-DB: " + ex.Message);
            }
        }
    }
}
