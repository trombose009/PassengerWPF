using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CateringWPF
{
    public partial class CateringWindow : Window
    {
        private readonly string[] dummyAvatars;
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>> seatPositions;
        private readonly System.Collections.Generic.Dictionary<string, double> baseRowY;
        private readonly System.Collections.Generic.Dictionary<string, double> avatarSize;

        public CateringWindow()
        {
            InitializeComponent();

            // Dummy Avatare aus Config-Pfad laden
            string avatarDir = ConfigService.Current.Paths.Avatars;
            if (System.IO.Directory.Exists(avatarDir))
            {
                dummyAvatars = System.IO.Directory.GetFiles(avatarDir, "*.png")
                                                 .Select(f => System.IO.Path.GetFileName(f))
                                                 .ToArray();
            }
            else
            {
                dummyAvatars = Array.Empty<string>();
            }

            // Sitz-Logik unverändert
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
            PlaceDummyPassengers();
        }

        private void SetupBackground()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            void AddImage(string fileName, int zIndex, bool hitTest = true)
            {
                var imgPath = System.IO.Path.Combine(cabinDir, fileName);
                if (!System.IO.File.Exists(imgPath)) return;

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
        }

        private void PlaceDummyPassengers()
        {
            var rand = new Random();
            string[] rows = { "back", "near", "front" };
            foreach (var row in rows)
            {
                var seats = seatPositions[row];
                string leftSeat = seats.Keys.First();
                string rightSeat = seats.Keys.Last();
                double y = baseRowY[row];

                CreateDummyAvatar(leftSeat, row, y, rand);
                CreateDummyAvatar(rightSeat, row, y, rand);
            }
        }

        private void CreateDummyAvatar(string seatLetter, string rowType, double y, Random rand)
        {
            double x = seatPositions[rowType][seatLetter];
            double size = avatarSize[rowType];

            if (dummyAvatars.Length == 0) return;
            string imgFile = dummyAvatars[rand.Next(dummyAvatars.Length)];

            string imgPath = System.IO.Path.Combine(ConfigService.Current.Paths.Avatars, imgFile);
            if (!System.IO.File.Exists(imgPath)) return;

            var img = new Image
            {
                Width = size,
                Height = size,
                Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute)),
                Tag = "avatar_dummy"
            };

            Canvas.SetLeft(img, x - size / 2);
            Canvas.SetTop(img, y);
            Panel.SetZIndex(img, rowType == "back" ? 2 : rowType == "near" ? 5 : 7);
            CabinCanvas.Children.Add(img);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = System.IO.Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!System.IO.File.Exists(path)) return null;
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            StartServiceButton.IsEnabled = false;

            string[] rowOrder = { "back", "near", "front" };

            await StewardessInitialMoveBack();

            foreach (var row in rowOrder)
            {
                double gateY = row switch
                {
                    "back" => baseRowY["front"] - 145,
                    "near" => baseRowY["near"] + 50,
                    "front" => baseRowY["front"] + 130,
                    _ => baseRowY["front"] - 145
                };

                await MoveStewardessToGangY(gateY);

                var leftPassenger = CabinCanvas.Children
                    .OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_dummy")
                    .Where(img => Canvas.GetTop(img) == baseRowY[row])
                    .OrderBy(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                if (leftPassenger != null)
                    await StewardessServePassenger(leftPassenger, true, row, nextRow: row);

                var rightPassenger = CabinCanvas.Children
                    .OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_dummy")
                    .Where(img => Canvas.GetTop(img) == baseRowY[row])
                    .OrderByDescending(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                if (rightPassenger != null)
                    await StewardessServePassenger(rightPassenger, false, row, nextRow: row);
            }

            await MoveStewardessToGangY(700);

            StartServiceButton.IsEnabled = true;
        }

        private async Task StewardessInitialMoveBack()
        {
            double speed = 6;
            double startX = 650;
            double startY = 700;
            double backY = baseRowY["front"] - 145;

            Stewardess.Source = LoadStewardessImage("stheck.png");
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            var scaleTransform = new ScaleTransform(2.7, 2.7);
            Stewardess.RenderTransform = scaleTransform;

            Canvas.SetLeft(Stewardess, startX);
            Canvas.SetTop(Stewardess, startY);
            Panel.SetZIndex(Stewardess, 10);

            while (Canvas.GetTop(Stewardess) > backY)
            {
                double newY = Canvas.GetTop(Stewardess) - speed;
                if (newY < backY) newY = backY;
                Canvas.SetTop(Stewardess, newY);

                double currentScale = 2.7 - (2.7 - 2.0) * ((startY - newY) / (startY - backY));
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;

                await Task.Delay(16);
            }

            scaleTransform.ScaleX = 2.0;
            scaleTransform.ScaleY = 2.0;
        }

        private async Task MoveStewardessToGangY(double targetY)
        {
            double speed = 6;

            Stewardess.Source = LoadStewardessImage("stfront.png");

            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(2.0, 2.0);
                Stewardess.RenderTransform = scaleTransform;
                Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            }

            double startY = Canvas.GetTop(Stewardess);
            double startScale = scaleTransform.ScaleY;

            double targetScale =
                (targetY < baseRowY["near"]) ? 2.0 :
                (targetY < baseRowY["front"]) ? 2.5 : 2.7;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 2)
            {
                double curY = Canvas.GetTop(Stewardess);
                curY += curY < targetY ? speed : -speed;
                Canvas.SetTop(Stewardess, curY);

                double progress = (curY - startY) / (targetY - startY);
                double currentScale = startScale + (targetScale - startScale) * progress;
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;

                await Task.Delay(16);
            }

            scaleTransform.ScaleX = targetScale;
            scaleTransform.ScaleY = targetScale;
            Canvas.SetTop(Stewardess, targetY);
        }

        private async Task StewardessServePassenger(Image passenger, bool isLeft, string row, string nextRow = null)
        {
            // Alle Bewegungslogik unverändert
            double speed = 6;

            double backY = baseRowY["front"] - 100;
            double yFixed = row switch
            {
                "back" => backY,
                "near" => baseRowY["near"] + 150,
                "front" => baseRowY["front"] + 200,
                _ => 700
            };

            double scale = row switch
            {
                "back" => 2.0,
                "near" => 2.5,
                "front" => 2.7,
                _ => 2.7
            };

            Panel.SetZIndex(Stewardess, row == "back" ? 3 : row == "near" ? 6 : 10);

            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(scale, scale);
                Stewardess.RenderTransform = scaleTransform;
                Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            }
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;

            double targetX = Canvas.GetLeft(passenger);

            Stewardess.Source = LoadStewardessImage(isLeft ? "stlinks.png" : "strechts.png");

            while (Math.Abs(Canvas.GetLeft(Stewardess) - targetX) > 2)
            {
                double currentX = Canvas.GetLeft(Stewardess);
                currentX += currentX < targetX ? speed : -speed;
                Canvas.SetLeft(Stewardess, currentX);
                Canvas.SetTop(Stewardess, yFixed);
                await Task.Delay(16);
            }

            Stewardess.Source = LoadStewardessImage(isLeft ? "stdservicelinks.png" : "stdservicerechts.png");
            await Task.Delay(3000);

            Stewardess.Source = LoadStewardessImage(isLeft ? "strechts.png" : "stlinks.png");

            double gangX = 650;
            while (Math.Abs(Canvas.GetLeft(Stewardess) - gangX) > 2)
            {
                double currentX = Canvas.GetLeft(Stewardess);
                currentX += currentX < gangX ? speed : -speed;
                Canvas.SetLeft(Stewardess, currentX);
                await Task.Delay(16);
            }

            Stewardess.Source = LoadStewardessImage("stfront.png");

            double endY;
            double scaleEnd;
            if (nextRow == null)
            {
                endY = 700;
                scaleEnd = 2.7;
            }
            else
            {
                endY = nextRow switch
                {
                    "back" => backY,
                    "near" => baseRowY["near"],
                    "front" => baseRowY["front"],
                    _ => 700
                };
                scaleEnd = nextRow switch
                {
                    "back" => 2.0,
                    "near" => 2.5,
                    "front" => 2.7,
                    _ => 2.7
                };
            }

            double startY = Canvas.GetTop(Stewardess);
            double scaleStart = scale;

            while (Canvas.GetTop(Stewardess) < endY)
            {
                double newY = Canvas.GetTop(Stewardess) + speed;
                if (newY > endY) newY = endY;
                Canvas.SetTop(Stewardess, newY);

                double progress = (newY - startY) / (endY - startY);
                double currentScale = scaleStart + (scaleEnd - scaleStart) * progress;
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;

                await Task.Delay(16);
            }

            scaleTransform.ScaleX = scaleEnd;
            scaleTransform.ScaleY = scaleEnd;

            Stewardess.Source = LoadStewardessImage("stfront.png");
        }
    }
}
