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
            LoadCateringCsv();
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

            if (File.Exists(avatarDbPath))
            {
                var lines = File.ReadAllLines(avatarDbPath).Skip(1);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                        avatarDb.Add(new AvatarDbItem { Name = parts[0], Avatar = parts[1] });
                }
            }
        }

        private void SaveAvatarDb()
        {
            string avatarDbPath = ConfigService.Current.Csv.AvatarDb;

            using var writer = new StreamWriter(avatarDbPath);
            writer.WriteLine("Name,Avatar");
            foreach (var item in avatarDb)
            {
                writer.WriteLine($"{item.Name},{item.Avatar}");
            }
        }

        private void LoadFlightData()
        {
            string actualFlightPath = ConfigService.Current.Csv.ActualFlight;
            if (!File.Exists(actualFlightPath)) return;

            var avatarFolder = ConfigService.Current.Paths.Avatars;

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string seat = parts[1];

                var existingDb = avatarDb.FirstOrDefault(x => x.Name == name);
                string avatar = existingDb?.Avatar ?? "default.png";
                if (existingDb == null)
                    avatarDb.Add(new AvatarDbItem { Name = name, Avatar = avatar });

                var passenger = new PassengerItem
                {
                    Name = name,
                    Sitzplatz = seat,
                    Avatar = avatar,
                    Orders = new[] { "placeholder.png", "placeholder.png", "placeholder.png", "placeholder.png" }
                };

                passenger.SetOrdersFolder(ConfigService.Current.Paths.Orders);
                passenger.UpdateAvatarImage(avatarFolder);
                cateringData.Add(passenger);
            }
        }

        private void SaveCateringCsv()
        {
            string cateringCsvPath = ConfigService.Current.Csv.Catering;
            if (cateringData.Count == 0) return;

            using var writer = new StreamWriter(cateringCsvPath);
            writer.WriteLine("Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
            foreach (var item in cateringData)
            {
                var orders = item.Orders.Select(o => string.IsNullOrEmpty(o) ? "placeholder.png" : o);
                writer.WriteLine($"{item.Name},{item.Sitzplatz},{item.Avatar},{string.Join(",", orders)}");
            }
        }

        private string GetRandomAvatar(string avatarFolder)
        {
            if (!Directory.Exists(avatarFolder))
                Directory.CreateDirectory(avatarFolder);

            var files = Directory.GetFiles(avatarFolder, "*.png").Select(Path.GetFileName).ToArray();
            return files.Length > 0 ? files[new Random().Next(files.Length)] : "placeholder.png";
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
                var avatarFolder = ConfigService.Current.Paths.Avatars;
                var dlg = new OpenFileDialog
                {
                    InitialDirectory = avatarFolder,
                    Filter = "PNG Dateien (*.png)|*.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    item.Avatar = Path.GetFileName(dlg.FileName);
                    item.UpdateAvatarImage(avatarFolder);

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
            ApplyActualFlightChanges();
            ProcessOrders();
            ReloadCateringCsvToGui();
        }

        private void ApplyActualFlightChanges()
        {
            string actualFlightPath = ConfigService.Current.Csv.ActualFlight;
            string avatarFolder = ConfigService.Current.Paths.Avatars;

            if (!File.Exists(actualFlightPath)) return;

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            var latestNames = lines.Select(l => l.Split(',')[0]).ToHashSet();

            // Entfernen nicht mehr vorhandener Passagiere
            var toRemove = cateringData.Where(cd => !latestNames.Contains(cd.Name)).ToList();
            foreach (var r in toRemove) cateringData.Remove(r);

            // Neue Passagiere hinzufügen
            foreach (var l in lines)
            {
                var parts = l.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string seat = parts[1];

                var existing = cateringData.FirstOrDefault(cd => cd.Name == name);
                if (existing == null)
                {
                    var db = avatarDb.FirstOrDefault(a => a.Name == name);
                    string avatar = db?.Avatar ?? "default.png";
                    if (db == null) avatarDb.Add(new AvatarDbItem { Name = name, Avatar = avatar });

                    var p = new PassengerItem
                    {
                        Name = name,
                        Sitzplatz = seat,
                        Avatar = avatar
                    };

                    // Orders aus catering.csv laden
                    var cateringCsvPath = ConfigService.Current.Csv.Catering;
                    if (File.Exists(cateringCsvPath))
                    {
                        var oldLine = File.ReadAllLines(cateringCsvPath).Skip(1)
                            .FirstOrDefault(x => x.Split(',')[0] == name);

                        if (oldLine != null)
                        {
                            var partsOld = oldLine.Split(',');
                            p.Orders = new[] { partsOld[3], partsOld[4], partsOld[5], partsOld[6] };
                        }
                        else
                        {
                            p.Orders = new string[4];
                        }
                    }
                    else
                    {
                        p.Orders = new[] { "placeholder.png", "placeholder.png", "placeholder.png", "placeholder.png" };
                    }

                    p.SetOrdersFolder(ConfigService.Current.Paths.Orders);
                    p.UpdateAvatarImage(avatarFolder);
                    cateringData.Add(p);
                }
                else
                {
                    existing.Sitzplatz = seat;
                    existing.UpdateAvatarImage(avatarFolder);
                }
            }
        }

        private void ProcessOrders()
        {
            string ordersCsvPath = ConfigService.Current.Csv.Orders;
            if (!File.Exists(ordersCsvPath)) return;

            var orderLines = File.ReadAllLines(ordersCsvPath).Skip(1).ToList();
            if (orderLines.Count == 0) return;

            foreach (var line in orderLines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string orderFile = parts[1];

                var pax = cateringData.FirstOrDefault(x => x.Name == name);
                if (pax == null) continue;

                var orders = pax.Orders.ToList();
                int freeIndex = orders.IndexOf("placeholder.png");
                if (freeIndex >= 0)
                    orders[freeIndex] = orderFile;
                else
                {
                    orders.RemoveAt(0);
                    orders.Add(orderFile);
                }

                pax.Orders = orders.ToArray();
            }

            SaveCateringCsv();
            File.WriteAllText(ordersCsvPath, "Name,order");
        }

        private void ReloadCateringCsvToGui()
        {
            string avatarFolder = ConfigService.Current.Paths.Avatars;
            string ordersFolder = ConfigService.Current.Paths.Orders;

            foreach (var pax in cateringData)
            {
                pax.SetOrdersFolder(ordersFolder);
                pax.UpdateAvatarImage(avatarFolder);
            }

            AvatarDataGrid.Items.Refresh();
        }

        private void LoadCateringCsv()
        {
            string catering = ConfigService.Current.Csv.Catering;
            if (!File.Exists(catering)) return;

            var lines = File.ReadAllLines(catering).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 7) continue;
                var pax = cateringData.FirstOrDefault(x => x.Name == parts[0]);
                if (pax != null)
                {
                    pax.Orders = new[] { parts[3], parts[4], parts[5], parts[6] };
                }
            }
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

        private string ordersFolder = "";

        public void SetOrdersFolder(string folder)
        {
            ordersFolder = folder;
        }

        public BitmapImage[] OrdersImages => Orders.Select(o => LoadImage(o, ordersFolder)).ToArray();

        public void UpdateAvatarImage(string folder)
        {
            AvatarImage = LoadImage(Avatar, folder);
        }

        private BitmapImage LoadImage(string fileName, string folder = "")
        {
            try
            {
                string path = string.IsNullOrEmpty(fileName) ? Path.Combine(folder, "placeholder.png") : Path.Combine(folder, fileName);
                if (!File.Exists(path))
                    path = Path.Combine(folder, "placeholder.png");

                return new BitmapImage(new Uri(Path.GetFullPath(path)));
            }
            catch
            {
                return new BitmapImage();
            }
        }
    }

    public class AvatarDbItem
    {
        public string Name { get; set; }
        public string Avatar { get; set; }
    }
}
