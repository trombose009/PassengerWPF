using PassengerWPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PassengerWPF
{
    public partial class CateringWindow : Window
    {
        private List<Passenger> passengers;
        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;

        public CateringWindow()
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
            LoadAndPlacePassengers();
        }

        private void SetupBackground()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            // Stewardess vorbereiten (im XAML deklariert)
            Stewardess.Source = LoadStewardessImage("stfront.png");
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(2.0, 2.0);
            Canvas.SetLeft(Stewardess, 650);
            Canvas.SetTop(Stewardess, 700);

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

            // Zuerst Hintergrund
            AddImage("seatsbackground.png", 1);

            // Dann Stewardess (bereits im XAML, kein Add nötig)

            // Dann Sitzreihen, die die Stewardess überdecken können
            AddImage("seatsmiddle.png", 4, false);
            AddImage("seatsfront.png", 7, false);
        }



        private (string, string) GetRowAndSeatLetter(string seat)
        {
            if (string.IsNullOrWhiteSpace(seat)) return ("", "");
            seat = seat.ToUpper().Trim();
            string seatLetter = seat[^1].ToString();
            int rowNumber = 0;
            _ = int.TryParse(seat[..^1], out rowNumber);

            if (rowNumber <= 5) return ("front", seatLetter);
            if (rowNumber <= 10) return ("near", seatLetter);
            return ("back", seatLetter);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path)) return null;
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        private void LoadAndPlacePassengers()
        {
            string csvPath = ConfigService.Current.Csv.PassengerData;
            passengers = PassengerDataService.LoadPassengers(csvPath);

            foreach (var passenger in passengers)
            {
                if (string.IsNullOrEmpty(passenger.AvatarFile) || string.IsNullOrEmpty(passenger.Sitzplatz))
                    continue;

                var (row, seatLetter) = GetRowAndSeatLetter(passenger.Sitzplatz);
                if (!seatPositions.ContainsKey(row) || !seatPositions[row].ContainsKey(seatLetter))
                    continue;

                double x = seatPositions[row][seatLetter];
                double y = baseRowY[row];
                double size = avatarSize[row];

                string imgPath = Path.Combine(ConfigService.Current.Paths.Avatars, passenger.AvatarFile);
                if (!File.Exists(imgPath))
                    continue;

                var img = new Image
                {
                    Width = size,
                    Height = size,
                    Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute)),
                    Tag = "avatar_passenger",
                    ToolTip = $"{passenger.Name}\nSitzplatz: {passenger.Sitzplatz}"
                };

                Canvas.SetLeft(img, x - size / 2);
                Canvas.SetTop(img, y);
                Panel.SetZIndex(img, row == "back" ? 2 : row == "near" ? 5 : 7);

                CabinCanvas.Children.Add(img);
            }
        }

        // -----------------------------
        // ORDER ICONS
        // -----------------------------
        private void ShowOrderIconsFor(Passenger passenger)
        {
            if (passenger == null) return;

            var avatarImg = CabinCanvas.Children
                .OfType<Image>()
                .FirstOrDefault(img => img.Tag?.ToString() == "avatar_passenger" &&
                                       img.ToolTip?.ToString().StartsWith(passenger.Name) == true);

            if (avatarImg == null) return;

            string badgeTag = $"order_badge_{passenger.Name}";
            var existing = CabinCanvas.Children.OfType<FrameworkElement>()
                .FirstOrDefault(x => (string)x.Tag == badgeTag);
            if (existing != null) CabinCanvas.Children.Remove(existing);

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
            double y = Canvas.GetTop(avatarImg) - grid - 12;

            Canvas.SetLeft(badge, x);
            Canvas.SetTop(badge, y);
            Panel.SetZIndex(badge, Panel.GetZIndex(avatarImg) + 10);

            // Fade-in
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var scale = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.7,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            badge.BeginAnimation(UIElement.OpacityProperty, fade);
            ((ScaleTransform)badge.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            ((ScaleTransform)badge.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scale);

            return badge;
        }

        // -----------------------------
        // SERVICE ANIMATION
        // -----------------------------
        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            StartServiceButton.IsEnabled = false;

            await StewardessInitialMoveBack();

            string[] rowOrder = { "back", "near", "front" };
            foreach (var row in rowOrder)
            {
                double rowY = row switch
                {
                    "back" => baseRowY["back"] + 100,
                    "near" => baseRowY["near"] + 105,
                    "front" => baseRowY["front"] + 110,
                    _ => 700
                };

                await MoveStewardessToGangY(rowY, row);

                var leftPassenger = CabinCanvas.Children.OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_passenger")
                    .Where(img => Math.Abs(Canvas.GetTop(img) - baseRowY[row]) < 1)
                    .OrderBy(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                var rightPassenger = CabinCanvas.Children.OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_passenger")
                    .Where(img => Math.Abs(Canvas.GetTop(img) - baseRowY[row]) < 1)
                    .OrderByDescending(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                if (leftPassenger != null)
                    await StewardessServePassenger(leftPassenger, true, row);

                if (rightPassenger != null)
                    await StewardessServePassenger(rightPassenger, false, row);
            }

            await MoveStewardessToGangY(700, ""); // zurück zum Ausgang
            StartServiceButton.IsEnabled = true;
        }

        private async Task StewardessInitialMoveBack()
        {
            double startX = 650;
            double startY = 700;
            double backY = baseRowY["front"] - 145;
            double startScale = 2.7;
            double endScale = 2.0;
            double speed = 3;

            Stewardess.Source = LoadStewardessImage("stheck.png");
            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(startScale, startScale);
                Stewardess.RenderTransform = scaleTransform;
            }

            await SmoothMoveY(backY, scaleTransform, startScale, endScale, speed, "back");
        }

        private async Task MoveStewardessToGangY(double targetY, string row)
        {
            double speed = 3;

            Stewardess.Source = LoadStewardessImage("stfront.png");

            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(2.0, 2.0);
                Stewardess.RenderTransform = scaleTransform;
            }

            double startScale = scaleTransform.ScaleY;
            double targetScale = (row == "back") ? 2.0 : (row == "near") ? 2.5 : 2.7;

            await SmoothMoveY(targetY, scaleTransform, startScale, targetScale, speed, row);
        }

        private async Task SmoothMoveY(double targetY, ScaleTransform scaleTransform, double startScale, double targetScale, double speed, string row = "")
        {
            double startY = Canvas.GetTop(Stewardess);
            int direction = (targetY > startY) ? 1 : -1;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 0.5)
            {
                double currentY = Canvas.GetTop(Stewardess) + direction * speed;
                if ((direction > 0 && currentY > targetY) || (direction < 0 && currentY < targetY))
                    currentY = targetY;

                Canvas.SetTop(Stewardess, currentY);

                // Dynamisch Z-Index nach Reihe
                switch (row)
                {
                    case "back":
                        Panel.SetZIndex(Stewardess, 3); // hinter seatsmiddle(4) und seatsfront(7)
                        break;
                    case "near":
                        Panel.SetZIndex(Stewardess, 6); // hinter seatsfront(7)
                        break;
                    case "front":
                        Panel.SetZIndex(Stewardess, 8); // vor allen
                        break;
                }

                double progress = Math.Abs(currentY - startY) / Math.Abs(targetY - startY);
                double scale = startScale + (targetScale - startScale) * progress;
                scaleTransform.ScaleX = scale;
                scaleTransform.ScaleY = scale;

                await Task.Delay(16);
            }

            Canvas.SetTop(Stewardess, targetY);
            scaleTransform.ScaleX = targetScale;
            scaleTransform.ScaleY = targetScale;

            // Finaler Z-Index
            switch (row)
            {
                case "back":
                    Panel.SetZIndex(Stewardess, 3);
                    break;
                case "near":
                    Panel.SetZIndex(Stewardess, 6);
                    break;
                case "front":
                    Panel.SetZIndex(Stewardess, 8);
                    break;
            }
        }


        private async Task SmoothMoveX(double targetX, double yFixed, ScaleTransform scaleTransform, double speed)
        {
            while (Math.Abs(Canvas.GetLeft(Stewardess) - targetX) > 0.5)
            {
                double currentX = Canvas.GetLeft(Stewardess);
                double delta = (targetX - currentX) * 0.08;
                if (Math.Abs(delta) < 0.3) delta = Math.Sign(delta) * 0.3;
                Canvas.SetLeft(Stewardess, currentX + delta);
                Canvas.SetTop(Stewardess, yFixed);

                await Task.Delay(20);
            }
            Canvas.SetLeft(Stewardess, targetX);
        }

        private async Task StewardessServePassenger(Image passenger, bool isLeft, string row)
        {
            double yFixed = row switch
            {
                "back" => baseRowY["back"] + 100,
                "near" => baseRowY["near"] + 105,
                "front" => baseRowY["front"] + 110,
                _ => 700
            };

            var p = passengers.FirstOrDefault(px => passenger.ToolTip?.ToString().StartsWith(px.Name) == true);

            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(2.0, 2.0);
                Stewardess.RenderTransform = scaleTransform;
            }

            double speed = 3;

            double targetX = Canvas.GetLeft(passenger);
            Stewardess.Source = LoadStewardessImage(isLeft ? "stlinks.png" : "strechts.png");

            await SmoothMoveX(targetX, yFixed, scaleTransform, speed);

            double originalX = Canvas.GetLeft(Stewardess);
            double amplitude = 6;
            for (int i = 0; i < 3; i++)
            {
                Canvas.SetLeft(Stewardess, originalX + amplitude);
                await Task.Delay(220);
                Canvas.SetLeft(Stewardess, originalX - amplitude);
                await Task.Delay(220);
            }
            Canvas.SetLeft(Stewardess, originalX);

            ShowOrderIconsFor(p);

            double gangX = 650;
            Stewardess.Source = LoadStewardessImage(isLeft ? "strechts.png" : "stlinks.png");
            await SmoothMoveX(gangX, yFixed, scaleTransform, speed);
            Stewardess.Source = LoadStewardessImage("stfront.png");
        }
    }
}
