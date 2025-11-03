using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Catering
{
    public partial class MainWindow : Window
    {
        private readonly string cateringCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catering", "catering.csv");
        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> rowScale;
        private readonly DispatcherTimer csvTimer;
        private string lastCsvText = "";

        public MainWindow()
        {
            InitializeComponent();

            // Sitzpositionen
            seatPositions = new()
            {
                ["back"] = new() { ["A"] = 280, ["B"] = 410, ["C"] = 535, ["D"] = 800, ["E"] = 930, ["F"] = 1060 },
                ["near"] = new() { ["A"] = 150, ["B"] = 320, ["C"] = 490, ["D"] = 860, ["E"] = 1020, ["F"] = 1190 },
                ["front"] = new() { ["B"] = 120, ["C"] = 390, ["D"] = 950, ["E"] = 1215 }
            };
            baseRowY = new() { ["back"] = 350, ["near"] = 420, ["front"] = 520 };
            rowScale = new() { ["back"] = 1.8, ["near"] = 2.2, ["front"] = 2.7 };

            // CSV prüfen alle 3 Sekunden
            csvTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            csvTimer.Tick += async (s, e) => await CheckCsvUpdates();
            csvTimer.Start();

            _ = RefreshPassengers();
        }

        private async Task<List<(string Name, string Seat, string Avatar)>> LoadCsv()
        {
            if (!File.Exists(cateringCsvPath)) return new();
            var lines = await File.ReadAllLinesAsync(cateringCsvPath);
            return lines.Skip(1)
                .Select(l => l.Split(','))
                .Where(p => p.Length >= 3)
                .Select(p => (p[0].Trim(), p[1].Trim(), p[2].Trim()))
                .ToList();
        }

        private string GetRowType(string seat)
        {
            if (string.IsNullOrWhiteSpace(seat)) return "back";
            var digits = new string(seat.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out int num)) return "back";
            if (num <= 12) return "back";
            if (num <= 20) return "near";
            return "front";
        }

        private void ClearAvatars()
        {
            var avatars = CabinCanvas.Children.OfType<Image>().Where(c => (c.Tag as string) == "avatar").ToList();
            foreach (var a in avatars) CabinCanvas.Children.Remove(a);
        }

        private async Task RefreshPassengers()
        {
            ClearAvatars();
            var passengers = await LoadCsv();

            foreach (var p in passengers)
            {
                string rowType = GetRowType(p.Seat);
                char seatLetterChar = p.Seat.Trim().Length > 0 ? p.Seat.Trim().Last() : 'A';
                string seatLetter = seatLetterChar.ToString().ToUpper();
                if (!seatPositions[rowType].ContainsKey(seatLetter))
                    seatLetter = seatPositions[rowType].Keys.First();

                double x = seatPositions[rowType][seatLetter];
                double y = baseRowY[rowType];
                string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", p.Avatar);
                string path = File.Exists(candidate) ? candidate : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "avatar1.png");

                var img = new Image
                {
                    Width = rowType == "front" ? 100 : rowType == "near" ? 80 : 60,
                    Height = rowType == "front" ? 100 : rowType == "near" ? 80 : 60,
                    Source = new BitmapImage(new Uri(path, UriKind.Absolute)),
                    Tag = "avatar",
                    Opacity = 0
                };
                Canvas.SetLeft(img, x - img.Width / 2);
                Canvas.SetTop(img, y);

                // ZIndex nach Reihe setzen
                Panel.SetZIndex(img, rowType == "back" ? 2 : rowType == "near" ? 5 : 7);

                CabinCanvas.Children.Add(img);

                // sanftes Einblenden
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
                img.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private async Task CheckCsvUpdates()
        {
            if (!File.Exists(cateringCsvPath)) return;
            string currentText = await File.ReadAllTextAsync(cateringCsvPath);
            if (currentText != lastCsvText)
            {
                lastCsvText = currentText;
                await RefreshPassengers();
            }
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            StartServiceButton.IsEnabled = false;
            await MoveStewardess();
            StartServiceButton.IsEnabled = true;
        }


        private readonly Dictionary<string, BitmapImage> stewardessImages = new();

        private BitmapImage GetStewardessImage(string filename)
        {
            if (!stewardessImages.ContainsKey(filename))
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", filename);
                stewardessImages[filename] = new BitmapImage(new Uri(path, UriKind.Absolute));
            }
            return stewardessImages[filename];
        }

        private async Task MoveStewardess()
        {
            double x = 650, y = 700;             // Startposition
            double targetY = baseRowY["near"];   // Ziel Y
            double scale = 2.7;
            double targetScale = 2.0;            // leicht verkleinern beim höchsten Punkt

            // ScaleTransform sicherstellen
            ScaleTransform transform;
            if (Stewardess.RenderTransform is ScaleTransform st)
                transform = st;
            else
            {
                transform = new ScaleTransform(scale, scale);
                Stewardess.RenderTransform = transform;
            }

            // --- Phase 1: Nach hinten / von uns weg (nach oben) ---
            Stewardess.Source = GetStewardessImage("stheck.png");
            while (y > targetY)
            {
                y -= 2;
                double progress = (700 - y) / (700 - targetY); // 0..1
                transform.ScaleX = transform.ScaleY = scale - (scale - targetScale) * progress;
                Canvas.SetTop(Stewardess, y);
                await Task.Delay(16);
            }

            await Task.Delay(200);

            // --- Phase 2: Nach links in den Gang (laufen) ---
            Stewardess.Source = GetStewardessImage("stlinks.png");
            double targetX = 400; // Beispiel Ziel X im Gang
            while (x > targetX)
            {
                x -= 2;
                Canvas.SetLeft(Stewardess, x);
                await Task.Delay(16);
            }

            await Task.Delay(200);

            // --- Phase 3: Servieren links (mit Wippbewegung) ---
            Stewardess.Source = GetStewardessImage("stdservicelinks.png");
            for (int i = 0; i < 10; i++)  // 10 Wippbewegungen
            {
                Canvas.SetTop(Stewardess, y - 5);
                await Task.Delay(100);
                Canvas.SetTop(Stewardess, y);
                await Task.Delay(100);
            }
            Canvas.SetTop(Stewardess, y); // sauber zurücksetzen

            await Task.Delay(200);

            // --- Phase 4: Zurück in den Gang (nach rechts) ---
            Stewardess.Source = GetStewardessImage("strechts.png");
            while (x < 650)
            {
                x += 2;
                Canvas.SetLeft(Stewardess, x);
                await Task.Delay(16);
            }

            // --- Phase 5: Zurück zum Ausgangspunkt (nach vorne / auf uns zu) ---
            Stewardess.Source = GetStewardessImage("stfront.png");
            while (y < 700)
            {
                y += 2;
                double progress = (y - targetY) / (700 - targetY);
                transform.ScaleX = transform.ScaleY = targetScale + (scale - targetScale) * progress;
                Canvas.SetTop(Stewardess, y);
                await Task.Delay(16);
            }

            // Transform zurücksetzen
            transform.ScaleX = transform.ScaleY = scale;
        }





    }
}
