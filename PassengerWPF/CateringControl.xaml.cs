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

namespace PassengerWPF
{
    public partial class CateringControl : UserControl
    {
        private List<Passenger> passengers;
        private MediaPlayer cateringMusicPlayer;
        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;

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

            SetupBackground();

            // Passagiere beim Loaded-Event platzieren
            this.Loaded += (s, e) => LoadAndPlacePassengers();
        }

        private void SetupBackground()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            void AddImage(string fileName, int zIndex, bool hitTest = true)
            {
                var imgPath = Path.Combine(cabinDir, fileName);
                if (!File.Exists(imgPath)) return;

                var img = new Image
                {
                    Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute)),
                    Width = 1355,
                    Height = 768,
                    IsHitTestVisible = hitTest
                };

                Panel.SetZIndex(img, zIndex);
                CabinCanvas.Children.Add(img);
            }

            AddImage("seatsbackground.png", 1);
            AddImage("seatsmiddle.png", 4, false);
            AddImage("seatsfront.png", 7, false);

            Stewardess.Source = LoadStewardessImage("stfront.png");
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(2.0, 2.0);
            Canvas.SetLeft(Stewardess, 650);
            Canvas.SetTop(Stewardess, 700);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path)) return null;
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        private (string row, string letter) GetRowAndSeatLetter(string seat)
        {
            if (string.IsNullOrWhiteSpace(seat)) return ("", "");
            seat = seat.ToUpper().Trim();
            string letter = seat[^1].ToString();
            int.TryParse(seat[..^1], out int rowNum);

            if (rowNum <= 5) return ("front", letter);
            if (rowNum <= 10) return ("near", letter);
            return ("back", letter);
        }

        private void LoadAndPlacePassengers()
        {
            // Alte Passagiere entfernen
            var oldElements = CabinCanvas.Children
                .OfType<FrameworkElement>()
                .Where(c => c.Tag != null &&
                            (c.Tag.ToString().StartsWith("avatar_passenger_") ||
                             c.Tag.ToString().StartsWith("name_") ||
                             c.Tag.ToString().StartsWith("order_badge_")))
                .ToList();

            foreach (var elem in oldElements)
                CabinCanvas.Children.Remove(elem);

            string csvPath = ConfigService.Current.Csv.PassengerData;
            string avatarsPath = ConfigService.Current.Paths.Avatars;

            var allSeats = seatPositions.SelectMany(r => r.Value.Keys.Select(l => r.Key + l)).ToList();
            passengers = PassengerDataService.LoadPassengers(csvPath, avatarsPath, allSeats)
                .Select(p => { p.Sitzplatz = p.Sitzplatz.Trim().ToUpper(); return p; })
                .ToList();

            var takenSeatsPerRow = new Dictionary<string, HashSet<string>>();
            foreach (var row in seatPositions.Keys)
                takenSeatsPerRow[row] = new HashSet<string>();

            foreach (var passenger in passengers)
            {
                if (string.IsNullOrEmpty(passenger.AvatarFile))
                    continue;

                var (row, seatLetter) = GetRowAndSeatLetter(passenger.Sitzplatz);

                // Nur freie Reihe wählen, wenn Sitzplatz leer oder ungültig
                if (!seatPositions.ContainsKey(row) || !seatPositions[row].ContainsKey(seatLetter) || takenSeatsPerRow[row].Contains(seatLetter))
                {
                    var freeRow = seatPositions
                        .OrderBy(r => Math.Abs(r.Value.Count - takenSeatsPerRow[r.Key].Count))
                        .FirstOrDefault(r => takenSeatsPerRow[r.Key].Count < r.Value.Count).Key;

                    if (freeRow != null)
                    {
                        row = freeRow;
                        var freeSeats = seatPositions[row].Keys.Except(takenSeatsPerRow[row]).ToList();
                        if (freeSeats.Count > 0)
                            seatLetter = freeSeats.First();
                        else
                            continue;
                    }
                }

                takenSeatsPerRow[row].Add(seatLetter);

                double x = seatPositions[row][seatLetter];
                double y = baseRowY[row];
                double size = avatarSize[row];

                string imgPath = Path.Combine(avatarsPath, passenger.AvatarFile);
                if (!File.Exists(imgPath)) continue;

                var img = new Image
                {
                    Width = size,
                    Height = size,
                    Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute)) { CacheOption = BitmapCacheOption.OnLoad },
                    Tag = $"avatar_passenger_{passenger.Name}"
                };

                Canvas.SetLeft(img, x - size / 2);
                Canvas.SetTop(img, y);
                Panel.SetZIndex(img, row == "back" ? 2 : row == "near" ? 5 : 8);
                CabinCanvas.Children.Add(img);

                // Namensschild
                var nameText = new TextBlock
                {
                    Text = passenger.Name,
                    Foreground = Brushes.Orange,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(4, 2, 4, 2)
                };

                var nameBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 30, 30, 30)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2, 1, 2, 1),
                    Child = nameText,
                    Tag = $"name_{passenger.Name}"
                };

                Canvas.SetLeft(nameBorder, x - size / 2);
                Canvas.SetTop(nameBorder, y - 22);
                Panel.SetZIndex(nameBorder, Panel.GetZIndex(img) + 5);
                CabinCanvas.Children.Add(nameBorder);
            }
        }

        private bool IsSeatLeft(string row, string letter)
        {
            letter = letter.ToUpper();
            return row switch
            {
                "back" => letter is "A" or "B" or "C",
                "near" => letter is "A" or "B" or "C",
                "front" => letter is "B" or "C",
                _ => true
            };
        }

        private void SetStewardessZIndex(string row)
        {
            switch (row)
            {
                case "back":
                    Panel.SetZIndex(Stewardess, 3);
                    break;
                case "near":
                    Panel.SetZIndex(Stewardess, 6);
                    break;
                case "front":
                    Panel.SetZIndex(Stewardess, 9);
                    break;
                default:
                    Panel.SetZIndex(Stewardess, 3);
                    break;
            }
        }

        // --- Stewardess-Animationen bleiben unverändert ---
        private async Task StewardessInitialMoveBack()
        {
            double targetY = baseRowY["front"] - 145;
            await MoveStewardessToGangY(targetY, "back", "stheck.png", 2.7);
        }

        private async Task MoveStewardessToGangY(double targetY, string row, string imageName = "stfront.png", double scale = 2.0)
        {
            Stewardess.Source = LoadStewardessImage(imageName);
            var transform = Stewardess.RenderTransform as ScaleTransform ?? new ScaleTransform(scale, scale);
            Stewardess.RenderTransform = transform;

            double startY = Canvas.GetTop(Stewardess);
            int direction = targetY > startY ? 1 : -1;
            double speed = 3;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 0.5)
            {
                double y = Canvas.GetTop(Stewardess) + direction * speed;
                if ((direction > 0 && y > targetY) || (direction < 0 && y < targetY)) y = targetY;

                Canvas.SetTop(Stewardess, y);
                SetStewardessZIndex(row);
                await Task.Delay(16);
            }

            Canvas.SetTop(Stewardess, targetY);
            SetStewardessZIndex(row);
        }

        private async Task StewardessServePassenger(Image passengerImg, string row)
        {
            var p = passengers.FirstOrDefault(px => passengerImg.Tag.ToString().Contains(px.Name));
            if (p == null) return;

            double yFixed = row switch
            {
                "back" => baseRowY["back"] + 100,
                "near" => baseRowY["near"] + 105,
                "front" => baseRowY["front"] + 110,
                _ => 700
            };

            double targetX = Canvas.GetLeft(passengerImg);
            string seatLetter = GetRowAndSeatLetter(p.Sitzplatz).letter;
            bool isLeft = IsSeatLeft(row, seatLetter);

            string reinImage = isLeft ? "stlinks.png" : "strechts.png";
            Stewardess.Source = LoadStewardessImage(reinImage);

            double startX = Canvas.GetLeft(Stewardess);
            while (Math.Abs(startX - targetX) > 0.5)
            {
                double delta = (targetX - startX) * 0.08;
                if (Math.Abs(delta) < 0.3) delta = Math.Sign(delta) * 0.3;
                startX += delta;

                Canvas.SetLeft(Stewardess, startX);
                Canvas.SetTop(Stewardess, yFixed);
                SetStewardessZIndex(row);

                await Task.Delay(16);
            }

            Canvas.SetLeft(Stewardess, targetX);

            string servImage = isLeft ? "stservicelinks.png" : "stservicerechts.png";
            Stewardess.Source = LoadStewardessImage(servImage);

            for (int i = 0; i < 3; i++)
            {
                Canvas.SetLeft(Stewardess, targetX + 6);
                await Task.Delay(220);
                Canvas.SetLeft(Stewardess, targetX - 6);
                await Task.Delay(220);
            }

            Canvas.SetLeft(Stewardess, targetX);
            ShowOrderIconsFor(p);

            string rausImage = isLeft ? "strechts.png" : "stlinks.png";
            Stewardess.Source = LoadStewardessImage(rausImage);

            double gangX = 650;
            while (Math.Abs(Canvas.GetLeft(Stewardess) - gangX) > 0.5)
            {
                double delta = (gangX - Canvas.GetLeft(Stewardess)) * 0.08;
                Canvas.SetLeft(Stewardess, Canvas.GetLeft(Stewardess) + delta);
                Canvas.SetTop(Stewardess, yFixed);
                SetStewardessZIndex(row);
                await Task.Delay(16);
            }

            Canvas.SetLeft(Stewardess, gangX);
            Stewardess.Source = LoadStewardessImage("stfront.png");
        }

        private void ShowOrderIconsFor(Passenger passenger)
        {
            if (passenger == null) return;

            var avatarImg = CabinCanvas.Children
                .OfType<Image>()
                .FirstOrDefault(img => img.Tag?.ToString() == $"avatar_passenger_{passenger.Name}");
            if (avatarImg == null) return;

            string badgeTag = $"order_badge_{passenger.Name}";
            var existing = CabinCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(x => (string)x.Tag == badgeTag);

            if (existing != null)
                CabinCanvas.Children.Remove(existing);

            var badge = PlaceOrderBadgeInternal(avatarImg, passenger, avatarImg.Width);
            badge.Tag = badgeTag;
            CabinCanvas.Children.Add(badge);
        }

        private Border PlaceOrderBadgeInternal(Image avatarImg, Passenger passenger, double avatarSize)
        {
            string ordersDir = ConfigService.Current.Paths.Orders;
            string[] files = { passenger.Order1, passenger.Order2, passenger.Order3, passenger.Order4 };
            double iconSize = 45;
            double padding = 2;
            double grid = iconSize * 2 + padding * 2;

            var badge = new Border
            {
                Width = grid,
                Height = grid,
                Background = new SolidColorBrush(Color.FromArgb(160, 30, 30, 30)),
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Opacity = 0,
                RenderTransform = new ScaleTransform(0.7, 0.7),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrWhiteSpace(files[i])) continue;
                string full = Path.Combine(ordersDir, files[i]);
                if (!File.Exists(full)) continue;

                var img = new Image
                {
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Source = new BitmapImage(new Uri(full, UriKind.Absolute))
                };

                Grid.SetRow(img, i / 2);
                Grid.SetColumn(img, i % 2);
                g.Children.Add(img);
            }

            badge.Child = g;

            double x = Canvas.GetLeft(avatarImg) + (avatarSize / 2) - (grid / 2);
            double y = Canvas.GetTop(avatarImg) - grid - 22;

            Canvas.SetLeft(badge, x);
            Canvas.SetTop(badge, y);
            Panel.SetZIndex(badge, Panel.GetZIndex(avatarImg) + 10);

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scale = new DoubleAnimation(0.7, 1, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            badge.BeginAnimation(UIElement.OpacityProperty, fade);
            ((ScaleTransform)badge.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            ((ScaleTransform)badge.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scale);

            return badge;
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (StartServiceButton != null)
                StartServiceButton.IsEnabled = false;

            string musicPath = ConfigService.Current.Paths.CateringMusic;
            if (File.Exists(musicPath))
            {
                if (cateringMusicPlayer == null)
                    cateringMusicPlayer = new MediaPlayer();

                cateringMusicPlayer.Open(new Uri(musicPath, UriKind.Absolute));
                cateringMusicPlayer.Volume = 0.5;
                cateringMusicPlayer.Play();
            }

            if (Stewardess != null)
                await StewardessInitialMoveBack();

            string[] rows = { "back", "near", "front" };
            foreach (var row in rows)
            {
                double rowY = row switch
                {
                    "back" => baseRowY["back"] + 100,
                    "near" => baseRowY["near"] + 105,
                    "front" => baseRowY["front"] + 110,
                    _ => 700
                };

                if (Stewardess != null)
                    await MoveStewardessToGangY(rowY, row);

                var passengersInRow = passengers?
                    .Where(p => GetRowAndSeatLetter(p.Sitzplatz).row == row)
                    .Where(p => !string.IsNullOrWhiteSpace(p.Order1) ||
                                !string.IsNullOrWhiteSpace(p.Order2) ||
                                !string.IsNullOrWhiteSpace(p.Order3) ||
                                !string.IsNullOrWhiteSpace(p.Order4))
                    .ToList() ?? new List<Passenger>();

                var passengerImages = passengersInRow
                    .Select(p => new
                    {
                        Passenger = p,
                        Image = CabinCanvas.Children
                                    .OfType<Image>()
                                    .FirstOrDefault(img => img.Tag?.ToString() == $"avatar_passenger_{p.Name}")
                    })
                    .Where(x => x.Image != null)
                    .OrderBy(x => Canvas.GetLeft(x.Image))
                    .ToList();

                foreach (var entry in passengerImages)
                {
                    if (entry.Image != null)
                        await StewardessServePassenger(entry.Image, row);
                }
            }

            if (Stewardess != null)
                await MoveStewardessToGangY(700, "");

            cateringMusicPlayer?.Stop();

            if (StartServiceButton != null)
                StartServiceButton.IsEnabled = true;
        }
    }
}
