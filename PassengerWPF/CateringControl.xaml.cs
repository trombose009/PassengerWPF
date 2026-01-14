using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PassengerWPF
{
    public partial class CateringControl : UserControl
    {
        private List<Passenger> passengers = new();

        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;

        private const double MiddleX = 620;
        private const double StartY = 800;

        // ✅ HIER stellst du ein, wie tief der Gang pro Reihe liegt (größer = weiter unten)
        private const double GangOffsetBack = 115;
        private const double GangOffsetNear = 105;
        private const double GangOffsetFront = 95;

        private string currentRowForZ = "front";

        private double GangY(string row) => row switch
        {
            "back" => baseRowY["back"] + GangOffsetBack,
            "near" => baseRowY["near"] + GangOffsetNear,
            "front" => baseRowY["front"] + GangOffsetFront,
            _ => StartY
        };

        private double RowScale(string row) => row switch
        {
            "front" => 5.0,
            "near" => 3.8,
            "back" => 2.7,
            _ => 5.0
        };

        public CateringControl()
        {
            InitializeComponent();

            seatPositions = new()
            {
                ["back"] = new() { ["A"] = 280, ["B"] = 410, ["C"] = 535, ["D"] = 800, ["E"] = 930, ["F"] = 1060 },
                ["near"] = new() { ["A"] = 150, ["B"] = 320, ["C"] = 490, ["D"] = 860, ["E"] = 1020, ["F"] = 1190 },
                ["front"] = new() { ["B"] = 120, ["C"] = 390, ["D"] = 950, ["E"] = 1215 }
            };

            baseRowY = new()
            {
                ["back"] = 350,
                ["near"] = 420,
                ["front"] = 520
            };

            avatarSize = new()
            {
                ["back"] = 60,
                ["near"] = 80,
                ["front"] = 100
            };

            SetupBackgroundAndStewardess();

            Loaded += async (_, __) =>
            {
                LoadAndPlacePassengers();
                await Task.Delay(200);
                _ = StartRouteSafeAsync();
            };
        }

        // -----------------------------
        // Layers + Stewardess init
        // -----------------------------
        private void SetupBackgroundAndStewardess()
        {
            CabinCanvas.Background = Brushes.Black;

            string cabinDir = ConfigService.Current.Paths.Cabin;

            void AddLayer(string fileName, int zIndex)
            {
                string path = Path.Combine(cabinDir, fileName);
                if (!File.Exists(path))
                {
                    Debug.WriteLine($"[LAYER] Missing: {path}");
                    return;
                }

                var img = new Image
                {
                    Source = LoadBitmapNoLock(path),
                    Width = 1355,
                    Height = 768,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(img, 0);
                Canvas.SetTop(img, 0);
                Panel.SetZIndex(img, zIndex);
                CabinCanvas.Children.Add(img);
            }

            // background (1) < st(back=3) < seatsmiddle (4) < st(near=6) < seatsfront (7) < st(front=9)
            AddLayer("seatsbackground.png", 1);
            AddLayer("seatsmiddle.png", 4);
            AddLayer("seatsfront.png", 7);

            Stewardess.Source = LoadStewardessImage("stfront.png");
            Stewardess.Width = 100;
            Stewardess.Height = 100;
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(RowScale("front"), RowScale("front"));

            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);

            currentRowForZ = "front";
            SetStewardessZIndex(currentRowForZ);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path))
            {
                Debug.WriteLine($"[ST] Missing: {path}");
                return null;
            }
            return LoadBitmapNoLock(path);
        }

        private static BitmapImage LoadBitmapNoLock(string absolutePath)
        {
            var bmp = new BitmapImage();
            using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();
            }
            return bmp;
        }

        // -----------------------------
        // Passengers
        // -----------------------------
        private void LoadAndPlacePassengers()
        {
            RemoveOldPassengerElements();

            string csvPath = ConfigService.Current.Csv.PassengerData;
            string avatarsPath = ConfigService.Current.Paths.Avatars;

            passengers = LoadPassengersFromCsv(csvPath).ToList();
            Debug.WriteLine($"[CSV] Loaded passengers: {passengers.Count}");

            var takenSeatsPerRow = seatPositions.Keys.ToDictionary(k => k, _ => new HashSet<string>());

            foreach (var p in passengers)
            {
                p.Name = (p.Name ?? "").Trim();
                p.Sitzplatz = (p.Sitzplatz ?? "").Trim().ToUpper();
                p.AvatarFile = (p.AvatarFile ?? "").Trim();

                if (string.IsNullOrWhiteSpace(p.AvatarFile))
                    continue;

                string imgPath = Path.Combine(avatarsPath, p.AvatarFile);
                if (!File.Exists(imgPath))
                    continue;

                var (row, seatLetter) = GetRowAndSeatLetter(p.Sitzplatz);
                if (string.IsNullOrWhiteSpace(row) || !seatPositions.ContainsKey(row))
                    row = "near";

                // falls SeatLetter nicht existiert: freien Seat in derselben Reihe nehmen
                if (!seatPositions[row].ContainsKey(seatLetter) || takenSeatsPerRow[row].Contains(seatLetter))
                {
                    var free = seatPositions[row].Keys.Except(takenSeatsPerRow[row]).ToList();
                    if (free.Count == 0) continue;
                    seatLetter = free[0];
                }

                takenSeatsPerRow[row].Add(seatLetter);

                double x = seatPositions[row][seatLetter];
                double y = baseRowY[row];
                double size = avatarSize[row];

                var avatar = new Image
                {
                    Width = size,
                    Height = size,
                    Source = LoadBitmapNoLock(imgPath),
                    Tag = $"avatar_passenger_{p.Name}"
                };

                Canvas.SetLeft(avatar, x - size / 2);
                Canvas.SetTop(avatar, y);

                Panel.SetZIndex(avatar, row switch
                {
                    "back" => 2,
                    "near" => 5,
                    "front" => 8,
                    _ => 5
                });

                CabinCanvas.Children.Add(avatar);
            }
        }

        private void RemoveOldPassengerElements()
        {
            var old = CabinCanvas.Children
                .OfType<FrameworkElement>()
                .Where(c => c.Tag is string t && t.StartsWith("avatar_passenger_"))
                .ToList();

            foreach (var e in old)
                CabinCanvas.Children.Remove(e);
        }

        private IEnumerable<Passenger> LoadPassengersFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Debug.WriteLine($"[CSV] Missing: {csvPath}");
                yield break;
            }

            var lines = File.ReadAllLines(csvPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (lines.Count < 2) yield break;

            for (int i = 1; i < lines.Count; i++)
            {
                var parts = lines[i].Split(',');

                yield return new Passenger
                {
                    Name = parts.ElementAtOrDefault(0)?.Trim() ?? "",
                    Sitzplatz = parts.ElementAtOrDefault(1)?.Trim() ?? "",
                    AvatarFile = parts.ElementAtOrDefault(2)?.Trim() ?? "",
                    // Orders optional – falls dein Passenger diese Props hat, ok; sonst ignorieren
                    Order1 = parts.ElementAtOrDefault(3)?.Trim() ?? "",
                    Order2 = parts.ElementAtOrDefault(4)?.Trim() ?? "",
                    Order3 = parts.ElementAtOrDefault(5)?.Trim() ?? "",
                    Order4 = parts.ElementAtOrDefault(6)?.Trim() ?? ""
                };
            }
        }

        private (string row, string letter) GetRowAndSeatLetter(string seat)
        {
            if (string.IsNullOrWhiteSpace(seat)) return ("", "");
            seat = seat.Trim().ToUpper();
            string letter = seat[^1].ToString();

            if (!int.TryParse(seat[..^1], out int rowNum))
                return ("", letter);

            if (rowNum <= 5) return ("front", letter);
            if (rowNum <= 10) return ("near", letter);
            return ("back", letter);
        }

        // -----------------------------
        // Z-Index für Stewardess
        // -----------------------------
        private void SetStewardessZIndex(string row)
        {
            currentRowForZ = row;

            int z = row switch
            {
                "back" => 3,   // unter seatsmiddle(4)
                "near" => 6,   // unter seatsfront(7)
                "front" => 9,  // über seatsfront(7)
                _ => 9
            };

            Panel.SetZIndex(Stewardess, z);
        }

        // -----------------------------
        // Route: gezielt zu JEDEM Passagier
        // -----------------------------
        private async Task StartRouteSafeAsync()
        {
            try { await RunServingRouteAsync(); }
            catch (Exception ex) { Debug.WriteLine("[ROUTE] EXCEPTION: " + ex); }
        }

        private async Task RunServingRouteAsync()
        {
            if (Stewardess == null) return;

            // ✅ Reihen, in denen überhaupt Passagiere sitzen (nicht nur Orders)
            var rowsWithPassengers = passengers
                .Select(p => GetRowAndSeatLetter(p.Sitzplatz).row)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            var routeRows = new List<string>();
            foreach (var r in new[] { "back", "near", "front" })
                if (rowsWithPassengers.Contains(r))
                    routeRows.Add(r);

            if (routeRows.Count == 0)
                return;

            // Start reset
            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);
            EnsureScaleTransform();
            SetStewardessZIndex("front");

            // Hoch zur hintersten relevanten Reihe (immer in der Mitte!)
            string topRow = routeRows[0];
            Canvas.SetLeft(Stewardess, MiddleX);

            await MoveStewardessVerticalWithScale(
                StartY,
                GangY(topRow),
                RowScale("front"),
                RowScale(topRow),
                topRow
            );

            // Dann Reihe für Reihe nach vorn
            for (int i = 0; i < routeRows.Count; i++)
            {
                string row = routeRows[i];

                // Immer zuerst in die Mitte
                await MoveStewardessHorizontal(MiddleX, row);

                // Exakt auf Reihen-Ganghöhe & Scale setzen
                await MoveStewardessVerticalWithScale(
                    Canvas.GetTop(Stewardess),
                    GangY(row),
                    ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                    RowScale(row),
                    row
                );

                // Passagiere dieser Reihe (links → rechts) – ✅ jeder wird besucht
                var paxInRow = passengers
                    .Where(p => GetRowAndSeatLetter(p.Sitzplatz).row == row)
                    .OrderBy(p => AvatarCenterX(p))
                    .ToList();

                foreach (var p in paxInRow)
                {
                    double x = AvatarCenterX(p);
                    if (x <= 0) continue;
                    await ServePassengerSimpleAsync(x, row);
                }

                // Zurück in die Mitte
                await MoveStewardessHorizontal(MiddleX, row);

                // Zur nächsten Reihe nach vorn (wieder Mitte fix!)
                if (i < routeRows.Count - 1)
                {
                    string nextRow = routeRows[i + 1];
                    Canvas.SetLeft(Stewardess, MiddleX);

                    await MoveStewardessVerticalWithScale(
                        Canvas.GetTop(Stewardess),
                        GangY(nextRow),
                        ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                        RowScale(nextRow),
                        nextRow
                    );
                }
            }

            // Zurück zur Startposition
            await MoveStewardessHorizontal(MiddleX, "front");
            await MoveStewardessVerticalWithScale(
                Canvas.GetTop(Stewardess),
                StartY,
                ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                RowScale("front"),
                "front"
            );

            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);
            var t = (ScaleTransform)Stewardess.RenderTransform;
            t.ScaleX = RowScale("front");
            t.ScaleY = RowScale("front");
            SetStewardessZIndex("front");
        }

        private double AvatarCenterX(Passenger p)
        {
            var avatar = FindAvatarOf(p);
            if (avatar == null) return 0;

            // ✅ Mitte statt linke Kante
            return Canvas.GetLeft(avatar) + (avatar.Width / 2.0);
        }

        private Image FindAvatarOf(Passenger p)
        {
            string tag = $"avatar_passenger_{(p.Name ?? "").Trim()}";
            return CabinCanvas.Children
                .OfType<Image>()
                .FirstOrDefault(i => (i.Tag as string) == tag);
        }

        // -----------------------------
        // “Serve”: rein → wackeln → raus
        // -----------------------------
        private async Task ServePassengerSimpleAsync(double passengerCenterX, string rowForZ)
        {
            await MoveStewardessHorizontal(passengerCenterX, rowForZ);

            for (int i = 0; i < 2; i++)
            {
                Canvas.SetLeft(Stewardess, passengerCenterX + 6);
                await Task.Delay(160);
                Canvas.SetLeft(Stewardess, passengerCenterX - 6);
                await Task.Delay(160);
            }
            Canvas.SetLeft(Stewardess, passengerCenterX);

            await MoveStewardessHorizontal(MiddleX, rowForZ);
        }

        // -----------------------------
        // Movement helpers
        // -----------------------------
        private void EnsureScaleTransform()
        {
            if (Stewardess.RenderTransform is not ScaleTransform)
                Stewardess.RenderTransform = new ScaleTransform(RowScale("front"), RowScale("front"));
        }

        private async Task MoveStewardessHorizontal(double targetX, string rowForZ)
        {
            SetStewardessZIndex(rowForZ);

            double speedFactor = 0.08;
            double minStep = 0.35;

            double x = Canvas.GetLeft(Stewardess);

            while (Math.Abs(x - targetX) > 0.5)
            {
                SetStewardessZIndex(rowForZ);

                double delta = (targetX - x) * speedFactor;
                if (Math.Abs(delta) < minStep) delta = Math.Sign(delta) * minStep;

                x += delta;
                Canvas.SetLeft(Stewardess, x);

                await Task.Delay(16);
            }

            Canvas.SetLeft(Stewardess, targetX);
            SetStewardessZIndex(rowForZ);
        }

        private async Task MoveStewardessVerticalWithScale(double startY, double targetY, double startScale, double targetScale, string rowForZ)
        {
            EnsureScaleTransform();
            var transform = (ScaleTransform)Stewardess.RenderTransform;

            SetStewardessZIndex(rowForZ);

            double speed = 3;
            int direction = targetY > startY ? 1 : -1;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 0.5)
            {
                SetStewardessZIndex(rowForZ);

                double currentY = Canvas.GetTop(Stewardess);
                double newY = currentY + direction * speed;

                if ((direction > 0 && newY > targetY) || (direction < 0 && newY < targetY))
                    newY = targetY;

                Canvas.SetTop(Stewardess, newY);

                double progress = Math.Abs(newY - startY) / Math.Abs(targetY - startY);
                if (double.IsNaN(progress) || double.IsInfinity(progress)) progress = 1;

                double scale = startScale + (targetScale - startScale) * progress;
                transform.ScaleX = scale;
                transform.ScaleY = scale;

                await Task.Delay(16);
            }

            Canvas.SetTop(Stewardess, targetY);
            transform.ScaleX = targetScale;
            transform.ScaleY = targetScale;

            SetStewardessZIndex(rowForZ);
        }
    }
}
