using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CateringWPF
{
    public partial class AvatarMappingWindow : Window
    {
        private readonly DispatcherTimer timer;
        private ObservableCollection<PassengerItem> cateringData = new();
        private ObservableCollection<AvatarDbItem> avatarDb = new();
        private const int updateInterval = 3000; // ms

        public AvatarMappingWindow()
        {
            InitializeComponent();

            LoadAvatarDb();
            LoadFlightData();
            SetupDataGrid();

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(updateInterval) };
            timer.Tick += Timer_Tick;
            timer.Start();

            this.Closed += (_, __) =>
            {
                timer.Stop();
                SaveCateringCsv();
                SaveAvatarDb();
            };
        }

        #region CSV Laden / Speichern

        private void LoadAvatarDb()
        {
            string avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            if (!File.Exists(avatarDbPath)) return;

            var lines = File.ReadAllLines(avatarDbPath).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                    avatarDb.Add(new AvatarDbItem { Name = parts[0], Avatar = parts[1] });
            }
        }

        private void SaveAvatarDb()
        {
            string avatarDbPath = ConfigService.Current.Csv.AvatarDb;
            var lines = avatarDb.Select(x => $"{x.Name},{x.Avatar}");
            File.WriteAllLines(avatarDbPath, new[] { "Name,Avatar" }.Concat(lines));
        }

        private void LoadFlightData()
        {
            string actualFlightPath = ConfigService.Current.Csv.ActualFlight;
            if (!File.Exists(actualFlightPath)) return;

            string avatarFolder = ConfigService.Current.Paths.Avatars;
            string orderFolder = ConfigService.Current.Paths.Orders;

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string seat = parts[1];
                string[] orders = parts.Skip(2).Take(4).ToArray();

                var existingDb = avatarDb.FirstOrDefault(x => x.Name == name);
                string avatar = existingDb?.Avatar ?? GetDefaultAvatar(avatarFolder);

                if (existingDb == null)
                    avatarDb.Add(new AvatarDbItem { Name = name, Avatar = avatar });

                var passenger = new PassengerItem
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatar,
                    Orders = orders
                };
                passenger.NormalizeOrders();
                passenger.UpdateAvatarImage(avatarFolder, orderFolder);

                cateringData.Add(passenger);
            }
        }

        private void SaveCateringCsv()
        {
            string cateringCsvPath = ConfigService.Current.Csv.Catering;
            using var writer = new StreamWriter(cateringCsvPath);
            writer.WriteLine("Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
            foreach (var item in cateringData)
            {
                item.NormalizeOrders();
                writer.WriteLine($"{item.Name},{item.Sitzplatz},{item.Avatar},{string.Join(",", item.Orders)}");
            }
        }

        private string GetDefaultAvatar(string folder)
        {
            string path = Path.Combine(folder, "default.png");
            return File.Exists(path) ? "default.png" : string.Empty;
        }

        #endregion

        #region DataGrid Setup

        private void SetupDataGrid()
        {
            AvatarDataGrid.ItemsSource = cateringData;

            AvatarDataGrid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding("Name") });
            AvatarDataGrid.Columns.Add(new DataGridTextColumn { Header = "Sitzplatz", Binding = new System.Windows.Data.Binding("Sitzplatz") });

            // Avatar Column
            var colAvatar = new DataGridTemplateColumn { Header = "Avatar" };
            var avatarFactory = new FrameworkElementFactory(typeof(Image));
            avatarFactory.SetValue(Image.WidthProperty, 64.0);
            avatarFactory.SetValue(Image.HeightProperty, 64.0);
            avatarFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("AvatarImage"));
            colAvatar.CellTemplate = new DataTemplate { VisualTree = avatarFactory };
            AvatarDataGrid.Columns.Add(colAvatar);

            // Button Column
            var colButton = new DataGridTemplateColumn { Header = "Avatar ändern" };
            var btnFactory = new FrameworkElementFactory(typeof(Button));
            btnFactory.SetValue(Button.ContentProperty, "Durchsuchen");
            btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(AvatarChangeButton_Click));
            colButton.CellTemplate = new DataTemplate { VisualTree = btnFactory };
            AvatarDataGrid.Columns.Add(colButton);

            // Orders Columns
            for (int i = 0; i < 4; i++)
            {
                var col = new DataGridTemplateColumn { Header = $"Order{i + 1}" };
                var imgFactory = new FrameworkElementFactory(typeof(Image));
                imgFactory.SetValue(Image.WidthProperty, 48.0);
                imgFactory.SetValue(Image.HeightProperty, 48.0);
                imgFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding($"OrdersImages[{i}]"));
                col.CellTemplate = new DataTemplate { VisualTree = imgFactory };
                AvatarDataGrid.Columns.Add(col);
            }
        }

        private void AvatarChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PassengerItem item)
            {
                string avatarFolder = ConfigService.Current.Paths.Avatars;
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = avatarFolder,
                    Filter = "PNG Dateien (*.png)|*.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    item.Avatar = Path.GetFileName(dlg.FileName);
                    item.UpdateAvatarImage(avatarFolder, ConfigService.Current.Paths.Orders);

                    var dbItem = avatarDb.FirstOrDefault(x => x.Name == item.Name);
                    if (dbItem != null) dbItem.Avatar = item.Avatar;
                    else avatarDb.Add(new AvatarDbItem { Name = item.Name, Avatar = item.Avatar });

                    AvatarDataGrid.Items.Refresh();
                    SaveAvatarDb();
                }
            }
        }

        #endregion

        #region Timer Tick

        private void Timer_Tick(object sender, EventArgs e)
        {
            string actualFlightPath = ConfigService.Current.Csv.ActualFlight;
            if (!File.Exists(actualFlightPath)) return;

            string avatarFolder = ConfigService.Current.Paths.Avatars;
            string orderFolder = ConfigService.Current.Paths.Orders;

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            var latest = lines.Select(l =>
            {
                var parts = l.Split(',');
                return new { Name = parts[0], Sitzplatz = parts[1], Orders = parts.Skip(2).Take(4).ToArray() };
            }).ToList();

            // Entfernen nicht mehr vorhandener Passagiere
            var toRemove = cateringData.Where(cd => !latest.Any(l => l.Name == cd.Name && l.Sitzplatz == cd.Sitzplatz)).ToList();
            foreach (var r in toRemove) cateringData.Remove(r);

            // Hinzufügen neuer Passagiere / Updates
            foreach (var l in latest)
            {
                var existing = cateringData.FirstOrDefault(cd => cd.Name == l.Name && cd.Sitzplatz == l.Sitzplatz);
                if (existing == null)
                {
                    var existingDb = avatarDb.FirstOrDefault(a => a.Name == l.Name);
                    string avatar = existingDb?.Avatar ?? GetDefaultAvatar(avatarFolder);
                    if (existingDb == null) avatarDb.Add(new AvatarDbItem { Name = l.Name, Avatar = avatar });

                    var passenger = new PassengerItem
                    {
                        Name = l.Name,
                        Sitzplatz = l.Sitzplatz,
                        Avatar = avatar,
                        Orders = l.Orders
                    };
                    passenger.NormalizeOrders();
                    passenger.UpdateAvatarImage(avatarFolder, orderFolder);
                    cateringData.Add(passenger);
                }
                else
                {
                    existing.Orders = l.Orders;
                    existing.NormalizeOrders();
                    existing.UpdateAvatarImage(avatarFolder, orderFolder);
                }
            }

            AvatarDataGrid.Items.Refresh();
            SaveCateringCsv();
        }

        #endregion
    }

    public class PassengerItem
    {
        public string Name { get; set; }
        public string Sitzplatz { get; set; }
        public string Avatar { get; set; }
        public string[] Orders { get; set; } = new string[4];

        public BitmapImage AvatarImage { get; set; }
        public BitmapImage[] OrdersImages { get; set; } = new BitmapImage[4];

        public void UpdateAvatarImage(string avatarFolder, string orderFolder)
        {
            AvatarImage = LoadImage(Avatar, avatarFolder, "default.png");
            for (int i = 0; i < 4; i++)
            {
                OrdersImages[i] = LoadImage(Orders[i], orderFolder, "placeholder.png");
            }
        }

        private BitmapImage LoadImage(string fileName, string folder, string fallback)
        {
            string path = string.IsNullOrEmpty(fileName) ? Path.Combine(folder, fallback) : Path.Combine(folder, fileName);
            if (!File.Exists(path)) path = Path.Combine(folder, fallback);

            if (!File.Exists(path))
                return new BitmapImage(); // leerer Platzhalter

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public void NormalizeOrders()
        {
            if (Orders == null) Orders = new string[4];
            if (Orders.Length < 4) Orders = Orders.Concat(Enumerable.Repeat(string.Empty, 4 - Orders.Length)).ToArray();
            if (Orders.Length > 4) Orders = Orders.Take(4).ToArray();
        }
    }

    public class AvatarDbItem
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
    }
}
