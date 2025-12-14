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
    public partial class AvatarMappingWindow : Window
    {
        private readonly string passengerDataPath;
        private readonly string avatarDbPath;
        private readonly string ordersCsvPath;
        private readonly string avatarDir;
        private readonly string ordersDir;

        private ObservableCollection<PassengerViewModel> passengers = new();

        public AvatarMappingWindow()
        {
            InitializeComponent();

            passengerDataPath = ConfigService.Current.Csv.PassengerData;
            avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            ordersCsvPath = ConfigService.Current.Csv.Orders;

            avatarDir = ConfigService.Current.Paths.Avatars;
            ordersDir = ConfigService.Current.Paths.Orders;

            LoadPassengers();
            DataGridPassengers.ItemsSource = passengers;
        }

        // ──────────────────────────────────────────────
        // Laden
        // ──────────────────────────────────────────────
        private void LoadPassengers()
        {
            passengers.Clear();

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

            if (!File.Exists(passengerDataPath))
                return;

            foreach (var line in File.ReadAllLines(passengerDataPath).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 7)
                    continue;

                string name = parts[0].Trim('"');
                string seat = parts[1].Trim('"');
                string avatarFile = parts[2].Trim('"');

                if (avatarDb.ContainsKey(name))
                    avatarFile = avatarDb[name];

                if (string.IsNullOrEmpty(avatarFile) ||
                    !File.Exists(Path.Combine(avatarDir, avatarFile)))
                    avatarFile = "placeholder.png";

                passengers.Add(new PassengerViewModel
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatarFile,
                    Order1 = parts[3],
                    Order2 = parts[4],
                    Order3 = parts[5],
                    Order4 = parts[6]
                });
            }
        }

        // ──────────────────────────────────────────────
        // Avatar ändern
        // ──────────────────────────────────────────────
        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel p)
                return;

            var dlg = new OpenFileDialog
            {
                Title = "Avatar auswählen",
                Filter = "PNG/JPG|*.png;*.jpg;*.jpeg",
                InitialDirectory = avatarDir
            };

            if (dlg.ShowDialog() == true)
            {
                p.Avatar = Path.GetFileName(dlg.FileName);
                p.AvatarImagePath = dlg.FileName;

                SaveAvatarDb();
                SavePassengerData();
            }
        }

        // ──────────────────────────────────────────────
        // Order manuell ändern
        // ──────────────────────────────────────────────
        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel p)
                return;

            if (!int.TryParse(btn.Tag?.ToString(), out int index))
                return;

            var dlg = new OpenFileDialog
            {
                Title = $"Order {index} auswählen",
                Filter = "PNG/JPG|*.png;*.jpg;*.jpeg",
                InitialDirectory = ordersDir
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

        // ──────────────────────────────────────────────
        // Avatar DB speichern
        // ──────────────────────────────────────────────
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

            foreach (var p in passengers)
                db[p.Name] = p.Avatar;

            using var w = new StreamWriter(avatarDbPath, false);
            w.WriteLine("Name,Avatar");
            foreach (var kv in db)
                w.WriteLine($"{kv.Key},{kv.Value}");
        }

        // ──────────────────────────────────────────────
        // PassengerData speichern (inkl. Orders!)
        // ──────────────────────────────────────────────
        private void SavePassengerData()
        {
            if (!File.Exists(passengerDataPath))
                return;

            var lines = File.ReadAllLines(passengerDataPath).ToList();
            if (lines.Count == 0)
                return;

            var header = lines[0];
            var map = lines.Skip(1)
                           .Select(l => l.Split(','))
                           .ToDictionary(p => p[0]);

            foreach (var p in passengers)
            {
                var parts = map.ContainsKey(p.Name)
                    ? map[p.Name]
                    : new string[7];

                Array.Resize(ref parts, 7);

                parts[0] = p.Name;
                parts[1] = p.Sitzplatz ?? "";
                parts[2] = p.Avatar ?? "";
                parts[3] = p.Order1 ?? "";
                parts[4] = p.Order2 ?? "";
                parts[5] = p.Order3 ?? "";
                parts[6] = p.Order4 ?? "";

                map[p.Name] = parts;
            }

            using var w = new StreamWriter(passengerDataPath, false);
            w.WriteLine(header);
            foreach (var p in map.Values)
                w.WriteLine(string.Join(",", p));
        }

        // ──────────────────────────────────────────────
        // 🔄 Refresh / Orders importieren
        // ──────────────────────────────────────────────
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Alte Passagierliste merken
            var oldPassengerNames = passengers.Select(p => p.Name).ToHashSet();

            // 1. passengerdata.csv neu laden (inklusive neuer Passagiere)
            LoadPassengers();

            // Neue Passagiere zählen
            var newPassengers = passengers.Count(p => !oldPassengerNames.Contains(p.Name));

            // 2. Orders importieren
            int importedOrders = 0;

            if (File.Exists(ordersCsvPath))
            {
                var lines = File.ReadAllLines(ordersCsvPath).ToList();
                if (lines.Count > 1)
                {
                    var orders = lines.Skip(1)
                                      .Select(l => l.Split(','))
                                      .Where(p => p.Length >= 2)
                                      .Select(p => new { Name = p[0].Trim(), Order = p[1].Trim() })
                                      .ToList();

                    foreach (var o in orders)
                    {
                        var p = passengers.FirstOrDefault(x => x.Name == o.Name);
                        if (p == null) continue;

                        if (string.IsNullOrEmpty(p.Order1)) p.Order1 = o.Order;
                        else if (string.IsNullOrEmpty(p.Order2)) p.Order2 = o.Order;
                        else if (string.IsNullOrEmpty(p.Order3)) p.Order3 = o.Order;
                        else if (string.IsNullOrEmpty(p.Order4)) p.Order4 = o.Order;
                        else continue;

                        importedOrders++;
                    }

                    // Orders speichern
                    SavePassengerData();

                    // orders.csv leeren
                    File.WriteAllText(ordersCsvPath, "Name,order" + Environment.NewLine);
                }
            }

            // 3. Meldung zusammenstellen
            string msg;
            if (importedOrders == 0 && newPassengers == 0)
                msg = "Keine neuen Orders oder Passagiere vorhanden.";
            else
            {
                msg = "";
                if (importedOrders > 0)
                    msg += $"{importedOrders} Orders importiert.\n";
                if (newPassengers > 0)
                    msg += $"{newPassengers} neue Passagiere hinzugefügt.";
            }

            MessageBox.Show(msg, "Refresh");
        }


    }
}
