using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace PassengerWPF
{
    public partial class AvatarMappingControl : UserControl
    {
        public ObservableCollection<PassengerViewModel> Passengers { get; set; } = new();

        private string passengerDataPath;
        private string avatarDbPath;
        private string avatarsPath;
        private string ordersCsvPath;
        private string ordersPath;

        public AvatarMappingControl()
        {
            InitializeComponent();

            passengerDataPath = ConfigService.Current.Csv.PassengerData;
            avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            avatarsPath = ConfigService.Current.Paths.Avatars;
            ordersPath = ConfigService.Current.Paths.Orders;
            ordersCsvPath = Path.Combine(ordersPath, ConfigService.Current.Csv.Orders); // flexible Datei aus Settings

            DgPassengers.ItemsSource = Passengers;
            RefreshPassengers();
        }

        private void RefreshPassengers()
        {
            LoadPassengers();
            ApplyOrdersToPassengers();
        }

        private void LoadPassengers()
        {
            Passengers.Clear();

            // Avatar-DB laden
            var avatarDb = new Dictionary<string, string>();
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

            foreach (var line in File.ReadAllLines(passengerDataPath).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 7) continue;
                Array.Resize(ref parts, 7);

                string name = parts[0].Trim('"');
                string seat = parts[1].Trim('"');
                string avatarFile = parts[2].Trim('"');
                string order1 = parts[3];
                string order2 = parts[4];
                string order3 = parts[5];
                string order4 = parts[6];

                if (avatarDb.ContainsKey(name))
                    avatarFile = avatarDb[name];

                if (string.IsNullOrEmpty(avatarFile) || !File.Exists(Path.Combine(avatarsPath, avatarFile)))
                    avatarFile = "placeholder.png";

                Passengers.Add(new PassengerViewModel
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatarFile,
                    AvatarImagePath = Path.Combine(avatarsPath, avatarFile),
                    Order1 = order1,
                    Order2 = order2,
                    Order3 = order3,
                    Order4 = order4,
                    Order1ImagePath = ResolveOrderPath(order1),
                    Order2ImagePath = ResolveOrderPath(order2),
                    Order3ImagePath = ResolveOrderPath(order3),
                    Order4ImagePath = ResolveOrderPath(order4)
                });
            }
        }

        private string ResolveOrderPath(string orderFile)
        {
            if (string.IsNullOrWhiteSpace(orderFile)) return null;
            var full = Path.Combine(ordersPath, orderFile);
            return File.Exists(full) ? full : null;
        }

        private Dictionary<string, List<string>> LoadOrdersCsv()
        {
            var result = new Dictionary<string, List<string>>();
            if (!File.Exists(ordersCsvPath)) return result;

            foreach (var line in File.ReadAllLines(ordersCsvPath).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var name = parts[0].Trim();
                var order = parts[1].Trim();

                if (!result.ContainsKey(name))
                    result[name] = new List<string>();

                result[name].Add(order);
            }

            return result;
        }

        private void ApplyOrdersToPassengers()
        {
            var orders = LoadOrdersCsv();

            foreach (var p in Passengers)
            {
                if (!orders.TryGetValue(p.Name, out var list))
                    continue;

                // Bestehende Orders aus passengerdata.csv prüfen
                var existingOrders = new string[] { p.Order1, p.Order2, p.Order3, p.Order4 };
                int orderIndex = 0;

                for (int i = 0; i < 4; i++)
                {
                    if (string.IsNullOrWhiteSpace(existingOrders[i]) && orderIndex < list.Count)
                    {
                        existingOrders[i] = list[orderIndex];
                        orderIndex++;
                    }
                }

                // Properties aktualisieren
                p.Order1 = existingOrders[0];
                p.Order2 = existingOrders[1];
                p.Order3 = existingOrders[2];
                p.Order4 = existingOrders[3];

                p.Order1ImagePath = ResolveOrderPath(p.Order1);
                p.Order2ImagePath = ResolveOrderPath(p.Order2);
                p.Order3ImagePath = ResolveOrderPath(p.Order3);
                p.Order4ImagePath = ResolveOrderPath(p.Order4);
            }

            // CSV leeren, Header bleibt erhalten
            if (File.Exists(ordersCsvPath))
            {
                var header = File.ReadAllLines(ordersCsvPath).FirstOrDefault() ?? "Name,Order";
                File.WriteAllText(ordersCsvPath, header + Environment.NewLine);
            }

            // Orders in passengerdata.csv schreiben
            SavePassengerData();
        }




        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            RefreshPassengers();
        }

        private void BtnAssignAvatar_Click(object sender, RoutedEventArgs e)
        {
            var files = Directory.Exists(avatarsPath) ? Directory.GetFiles(avatarsPath, "*.png") : new string[0];
            if (files.Length == 0) return;

            var rnd = new Random();
            foreach (PassengerViewModel p in DgPassengers.SelectedItems)
            {
                p.Avatar = Path.GetFileName(files[rnd.Next(files.Length)]);
                p.AvatarImagePath = Path.Combine(avatarsPath, p.Avatar);
            }

            SaveAvatarDb();
            SavePassengerData();
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel p) return;

            var dlg = new OpenFileDialog
            {
                Title = "Avatar auswählen",
                Filter = "PNG/JPG|*.png;*.jpg;*.jpeg",
                InitialDirectory = avatarsPath
            };

            if (dlg.ShowDialog() == true)
            {
                p.Avatar = Path.GetFileName(dlg.FileName);
                p.AvatarImagePath = dlg.FileName;

                SaveAvatarDb();
                SavePassengerData();
            }
        }

        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel p) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int index)) return;

            var dlg = new OpenFileDialog
            {
                Title = $"Order {index} auswählen",
                Filter = "PNG/JPG|*.png;*.jpg;*.jpeg",
                InitialDirectory = ordersPath
            };

            if (dlg.ShowDialog() == true)
            {
                string file = Path.GetFileName(dlg.FileName);
                switch (index)
                {
                    case 1: p.Order1 = file; p.Order1ImagePath = dlg.FileName; break;
                    case 2: p.Order2 = file; p.Order2ImagePath = dlg.FileName; break;
                    case 3: p.Order3 = file; p.Order3ImagePath = dlg.FileName; break;
                    case 4: p.Order4 = file; p.Order4ImagePath = dlg.FileName; break;
                }

                SavePassengerData();
            }
        }

        private void SaveAvatarDb()
        {
            var db = new Dictionary<string, string>();
            if (File.Exists(avatarDbPath))
            {
                foreach (var line in File.ReadAllLines(avatarDbPath).Skip(1))
                {
                    var p = line.Split(',');
                    if (p.Length >= 2)
                        db[p[0]] = p[1];
                }
            }

            foreach (var p in Passengers)
                db[p.Name] = p.Avatar;

            using var w = new StreamWriter(avatarDbPath, false);
            w.WriteLine("Name,Avatar");
            foreach (var kv in db)
                w.WriteLine($"{kv.Key},{kv.Value}");
        }

        private void SavePassengerData()
        {
            if (!File.Exists(passengerDataPath)) return;

            var lines = File.ReadAllLines(passengerDataPath).ToList();
            if (lines.Count == 0) return;

            var header = lines[0];
            var map = lines.Skip(1)
                           .Select(l =>
                           {
                               var p = l.Split(',');
                               Array.Resize(ref p, 7);
                               return p;
                           })
                           .ToDictionary(p => p[0]);

            foreach (var p in Passengers)
            {
                if (!map.ContainsKey(p.Name)) continue;

                var parts = map[p.Name];
                parts[0] = p.Name;
                parts[1] = p.Sitzplatz ?? "";
                parts[2] = p.Avatar ?? "";
                parts[3] = p.Order1 ?? parts[3];
                parts[4] = p.Order2 ?? parts[4];
                parts[5] = p.Order3 ?? parts[5];
                parts[6] = p.Order4 ?? parts[6];

                map[p.Name] = parts;
            }

            using var w = new StreamWriter(passengerDataPath, false);
            w.WriteLine(header);
            foreach (var p in map.Values)
                w.WriteLine(string.Join(",", p));
        }
    }
}
