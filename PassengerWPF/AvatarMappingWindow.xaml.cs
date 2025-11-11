using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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

            passengerDataPath = ConfigService.Current.Csv.PassengerData;
            avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            avatarDir = ConfigService.Current.Paths.Avatars;
            ordersDir = ConfigService.Current.Paths.Orders;

            LoadPassengers();
            DataGridPassengers.ItemsSource = passengers;
        }

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

                passengers.Add(new PassengerViewModel
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatarFile,
                    Orders = new string[] { parts[3], parts[4], parts[5], parts[6] }
                });
            }
        }

        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel passenger) return;

            var dlg = new OpenFileDialog
            {
                Title = "Avatar auswählen",
                Filter = "PNG Dateien|*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                Dispatcher.Invoke(() =>
                {
                    string fileName = Path.GetFileNameWithoutExtension(dlg.FileName);
                    string ext = Path.GetExtension(dlg.FileName);
                    string destFile;
                    int counter = 1;

                    do
                    {
                        string newName = counter == 1 ? $"{fileName}{ext}" : $"{fileName}_{counter}{ext}";
                        destFile = Path.Combine(avatarDir, newName);
                        counter++;
                    } while (File.Exists(destFile));

                    File.Copy(dlg.FileName, destFile, true);

                    passenger.Avatar = Path.GetFileName(destFile);
                    passenger.AvatarImagePath = destFile;

                    SaveAvatarDb();
                }, DispatcherPriority.Background);
            }
        }

        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PassengerViewModel passenger) return;

            int orderIndex = (int)btn.Tag;

            var dlg = new OpenFileDialog
            {
                Title = "Order-Bild auswählen",
                Filter = "PNG Dateien|*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                Dispatcher.Invoke(() =>
                {
                    string fileName = Path.GetFileNameWithoutExtension(dlg.FileName);
                    string ext = Path.GetExtension(dlg.FileName);
                    string destFile;
                    int counter = 1;

                    do
                    {
                        string newName = counter == 1 ? $"{fileName}{ext}" : $"{fileName}_{counter}{ext}";
                        destFile = Path.Combine(ordersDir, newName);
                        counter++;
                    } while (File.Exists(destFile));

                    File.Copy(dlg.FileName, destFile, true);

                    passenger.Orders[orderIndex] = Path.GetFileName(destFile);
                    passenger.OrderImagePaths[orderIndex] = destFile;

                    SavePassengerData();
                }, DispatcherPriority.Background);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SavePassengerData();
            SaveAvatarDb();
            MessageBox.Show("PassengerData und Avatar-DB gespeichert!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SavePassengerData()
        {
            using var writer = new StreamWriter(passengerDataPath);
            writer.WriteLine("Name,Sitzplatz,Avatar,Order1,Order2,Order3,Order4");

            foreach (var p in passengers)
            {
                writer.WriteLine($"{p.Name},{p.Sitzplatz},{p.Avatar},{string.Join(",", p.Orders)}");
            }
        }

        private void SaveAvatarDb()
        {
            using var writer = new StreamWriter(avatarDbPath);
            writer.WriteLine("Name,Avatar");

            foreach (var p in passengers)
            {
                writer.WriteLine($"{p.Name},{p.Avatar}");
            }
        }

        public class PassengerViewModel
        {
            public string Name { get; set; }
            public string Sitzplatz { get; set; }

            private string avatar;
            public string Avatar
            {
                get => avatar;
                set
                {
                    avatar = value;
                    AvatarImagePath = Path.Combine(ConfigService.Current.Paths.Avatars, value);
                }
            }

            public string AvatarImagePath { get; set; }

            public string[] Orders { get; set; } = new string[4];

            public string[] OrderImagePaths { get; set; } = new string[4];
        }
    }
}
