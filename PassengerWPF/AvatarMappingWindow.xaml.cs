using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PassengerWPF
{
    public partial class AvatarMappingWindow : Window
    {
        private readonly DispatcherTimer timer;
        private ObservableCollection<PassengerItem> passengerData = new();
        private ObservableCollection<AvatarDbItem> avatarDb = new();
        private const int updateInterval = 3000; // ms

        public AvatarMappingWindow()
        {
            InitializeComponent();

            LoadAvatarDb();
            LoadPassengerDataCsv();
            SetupDataGrid();

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(updateInterval) };
            timer.Tick += Timer_Tick;
            timer.Start();

            this.Closed += (_, __) =>
            {
                timer.Stop();
                SavePassengerDataCsv();
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

        private void LoadPassengerDataCsv()
        {
            string passengerCsvPath = ConfigService.Current.Csv.PassengerData;
            if (!File.Exists(passengerCsvPath)) return;

            var avatarFolder = ConfigService.Current.Paths.Avatars;

            var lines = File.ReadAllLines(passengerCsvPath).Skip(1);
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
                passengerData.Add(passenger);
            }
        }

        private void SavePassengerDataCsv()
        {
            string passengerCsvPath = ConfigService.Current.Csv.PassengerData;
            if (passengerData.Count == 0) return;

            using var writer = new StreamWriter(passengerCsvPath);
            writer.WriteLine("Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");
            foreach (var item in passengerData)
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
            AvatarDataGrid.ItemsSource = passengerData;

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

            // Avatar ändern Button
            var colButton = new DataGridTemplateColumn { Header = "Avatar ändern" };
            var btnFactory = new FrameworkElementFactory(typeof(Button));
            btnFactory.SetValue(Button.ContentProperty, "Durchsuchen");
            btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(AvatarChangeButton_Click));
            colButton.CellTemplate = new DataTemplate { VisualTree = btnFactory };
            AvatarDataGrid.Columns.Add(colButton);

            // Orders Columns
            for (int i = 0; i < 4; i++)
            {
                int index = i; // closure-safe
                var col = new DataGridTemplateColumn { Header = $"Order{i + 1}" };
                var imgFactory = new FrameworkElementFactory(typeof(Image));
                imgFactory.SetValue(Image.WidthProperty, 48.0);
                imgFactory.SetValue(Image.HeightProperty, 48.0);
                imgFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding($"OrdersImages[{i}]"));
                col.CellTemplate = new DataTemplate { VisualTree = imgFactory };
                AvatarDataGrid.Columns.Add(col);
            }

            // ContextMenu für Rechtsklick auf jede Zeile
            AvatarDataGrid.LoadingRow += (s, e) =>
            {
                e.Row.ContextMenu = CreateRowContextMenu();
                e.Row.ContextMenu.DataContext = e.Row.DataContext;
            };
        }

        private ContextMenu CreateRowContextMenu()
        {
            var menu = new ContextMenu();
            for (int i = 0; i < 4; i++)
            {
                int index = i; // closure-safe
                var item = new MenuItem { Header = $"Order {i + 1} auswählen..." };
                item.Click += (s, e) =>
                {
                    if ((s as MenuItem)?.DataContext is PassengerItem pax)
                    {
                        var ordersFolder = ConfigService.Current.Paths.Orders;
                        var dlg = new OpenFileDialog
                        {
                            InitialDirectory = ordersFolder,
                            Filter = "PNG Dateien (*.png)|*.png"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            pax.Orders[index] = Path.GetFileName(dlg.FileName);
                            pax.SetOrdersFolder(ordersFolder);
                            AvatarDataGrid.Items.Refresh();
                            SavePassengerDataCsv();
                        }
                    }
                };
                menu.Items.Add(item);
            }
            return menu;
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
            RefreshOrdersOnly();
        }

        private void RefreshOrdersOnly()
        {
            foreach (var pax in passengerData)
            {
                pax.SetOrdersFolder(ConfigService.Current.Paths.Orders);
                pax.UpdateAvatarImage(ConfigService.Current.Paths.Avatars);
            }
            AvatarDataGrid.Items.Refresh();
        }

        private void ApplyActualFlightChanges()
        {
            string actualFlightPath = ConfigService.Current.Csv.ActualFlight;
            string avatarFolder = ConfigService.Current.Paths.Avatars;

            if (!File.Exists(actualFlightPath)) return;

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            var latestNames = lines.Select(l => l.Split(',')[0]).ToHashSet();

            var toRemove = passengerData.Where(cd => !latestNames.Contains(cd.Name)).ToList();
            foreach (var r in toRemove) passengerData.Remove(r);

            foreach (var l in lines)
            {
                var parts = l.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string seat = parts[1];

                var existing = passengerData.FirstOrDefault(cd => cd.Name == name);
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

                    var passengerCsvPath = ConfigService.Current.Csv.PassengerData;
                    if (File.Exists(passengerCsvPath))
                    {
                        var oldLine = File.ReadAllLines(passengerCsvPath).Skip(1)
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
                    passengerData.Add(p);
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

                var pax = passengerData.FirstOrDefault(x => x.Name == name);
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

            SavePassengerDataCsv();
            File.WriteAllText(ordersCsvPath, "Name,order");
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
