using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Catering
{
    public partial class MainWindow : Window
    {
        private readonly string[] dummyAvatars = new[]
        {
            "avatar1.png","avatar2.png","avatar3.png","avatar4.png","avatar5.png","avatar6.png"
        };

        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, double>> seatPositions
            = new()
            {
                ["back"] = new() { ["A"] = 280, ["B"] = 410, ["C"] = 535, ["D"] = 800, ["E"] = 930, ["F"] = 1060 },
                ["near"] = new() { ["A"] = 150, ["B"] = 320, ["C"] = 490, ["D"] = 860, ["E"] = 1020, ["F"] = 1190 },
                ["front"] = new() { ["B"] = 120, ["C"] = 390, ["D"] = 950, ["E"] = 1215 }
            };

        private readonly System.Collections.Generic.Dictionary<string, double> baseRowY = new()
        {
            ["back"] = 350,
            ["near"] = 420,
            ["front"] = 520
        };

        private readonly System.Collections.Generic.Dictionary<string, double> avatarSize = new()
        {
            ["back"] = 60,
            ["near"] = 80,
            ["front"] = 100
        };

        public MainWindow()
        {
            InitializeComponent();
            SetupBackground();
            PlaceDummyPassengers();
        }

        private void SetupBackground()
        {
            var bg = new Image
            {
                Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "seatsbackground.png"), UriKind.Absolute)),
                Width = 1355,
                Height = 768
            };
            Panel.SetZIndex(bg, 1);
            CabinCanvas.Children.Add(bg);

            var middle = new Image
            {
                Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "seatsmiddle.png"), UriKind.Absolute)),
                Width = 1355,
                Height = 768,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(middle, 4);
            CabinCanvas.Children.Add(middle);

            var front = new Image
            {
                Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "seatsfront.png"), UriKind.Absolute)),
                Width = 1355,
                Height = 768,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(front, 7);
            CabinCanvas.Children.Add(front);
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

            string imgFile = dummyAvatars[rand.Next(dummyAvatars.Length)];
            var img = new Image
            {
                Width = size,
                Height = size,
                Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imgFile), UriKind.Absolute)),
                Tag = "avatar_dummy"
            };

            Canvas.SetLeft(img, x - size / 2);
            Canvas.SetTop(img, y);
            Panel.SetZIndex(img, rowType == "back" ? 2 : rowType == "near" ? 5 : 7);
            CabinCanvas.Children.Add(img);
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            StartServiceButton.IsEnabled = false;

            string[] rowOrder = { "back", "near", "front" };

            // 1) Stewardess GANZ nach hinten → wie bei dir bisher
            await StewardessInitialMoveBack();

            // 2) pro Reihe immer: zurück in Gang und von dort nach vorne in die Reihe
            foreach (var row in rowOrder)
            {
                // bevor wir den ersten Passagier der Reihe bedienen:
                // bitte immer wieder an den Gang / den "Row-Gate" der Reihe laufen

                // das ist der Y Wert der Gangposition dieser Reihe (wie wir ihn beim initialen Back-Y hatten)
                double gateY = row switch
                {
                    "back" => baseRowY["front"] - 145,
                    "near" => baseRowY["near"] + 50,
                    "front" => baseRowY["front"] + 130,
                    _ => baseRowY["front"] - 145
                };

                await MoveStewardessToGangY(gateY);   // <---- NEU

                // LINKS
                var leftPassenger = CabinCanvas.Children
                    .OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_dummy")
                    .Where(img => Canvas.GetTop(img) == baseRowY[row])
                    .OrderBy(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                if (leftPassenger != null)
                    await StewardessServePassenger(leftPassenger, true, row, nextRow: row);

                // RECHTS
                var rightPassenger = CabinCanvas.Children
                    .OfType<Image>()
                    .Where(img => img.Tag?.ToString() == "avatar_dummy")
                    .Where(img => Canvas.GetTop(img) == baseRowY[row])
                    .OrderByDescending(img => Canvas.GetLeft(img))
                    .FirstOrDefault();

                if (rightPassenger != null)
                    await StewardessServePassenger(rightPassenger, false, row, nextRow: row);
            }

            // 3) am Ende ins Finish (Gang ganz vorne)
            await MoveStewardessToGangY(700);

            StartServiceButton.IsEnabled = true;
        }


        private async Task StewardessInitialMoveBack()
        {
            double speed = 6;
            double startX = 650;
            double startY = 700;
            double backY = baseRowY["front"] - 145;

            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "stheck.png"), UriKind.Absolute));
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

                // Skalierung interpolieren
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

            // IMMER frontal Bild
            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images", "stfront.png"), UriKind.Absolute));

            // welche aktuelle Skalierung?
            var scaleTransform = Stewardess.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(2.0, 2.0);
                Stewardess.RenderTransform = scaleTransform;
                Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            }

            double startY = Canvas.GetTop(Stewardess);
            double startScale = scaleTransform.ScaleY; // X = Y identisch bei dir

            // Zielskala abhängig von Ziel-Y (wie bei dir im Code)
            double targetScale =
                (targetY < baseRowY["near"]) ? 2.0 :
                (targetY < baseRowY["front"]) ? 2.5 : 2.7;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 2)
            {
                double curY = Canvas.GetTop(Stewardess);
                curY += curY < targetY ? speed : -speed;
                Canvas.SetTop(Stewardess, curY);

                // interpolieren
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
            double speed = 6;

            // Y-Position für die jeweilige Reihe
            double backY = baseRowY["front"] - 100;
            double yFixed = row switch
            {
                "back" => backY,
                "near" => baseRowY["near"] + 150,
                "front" => baseRowY["front"] + 200,
                _ => 700
            };

            // Skalierung je nach Reihe
            double scale = row switch
            {
                "back" => 2.0,
                "near" => 2.5,
                "front" => 2.7,
                _ => 2.7
            };

            // Z-Index passend zur Reihe
            Panel.SetZIndex(Stewardess, row == "back" ? 3 : row == "near" ? 6 : 10);

            // Transform initialisieren
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

            // Schritt a: Horizontal laufen zum Passagier (seitlich)
            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images",
                isLeft ? "stlinks.png" : "strechts.png"), UriKind.Absolute));

            while (Math.Abs(Canvas.GetLeft(Stewardess) - targetX) > 2)
            {
                double currentX = Canvas.GetLeft(Stewardess);
                currentX += currentX < targetX ? speed : -speed;
                Canvas.SetLeft(Stewardess, currentX);
                Canvas.SetTop(Stewardess, yFixed);
                await Task.Delay(16);
            }

            // Schritt b: Servieren
            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images",
                isLeft ? "stdservicelinks.png" : "stdservicerechts.png"), UriKind.Absolute));
            await Task.Delay(3000);

            // Schritt c: Rückweg horizontal zum Gang (seitlich)
            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images",
                isLeft ? "strechts.png" : "stlinks.png"), UriKind.Absolute)); // abhängig von Richtung

            double gangX = 650;
            while (Math.Abs(Canvas.GetLeft(Stewardess) - gangX) > 2)
            {
                double currentX = Canvas.GetLeft(Stewardess);
                currentX += currentX < gangX ? speed : -speed;
                Canvas.SetLeft(Stewardess, currentX);
                await Task.Delay(16);
            }

            // Schritt d: Vertikal vom Gang auf uns zu (frontal)
            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images", "stfront.png"), UriKind.Absolute));

            double endY;
            double scaleEnd;
            if (nextRow == null) // letzte Reihe
            {
                endY = 700;
                scaleEnd = 2.7;
            }
            else // bis zur Gangposition der nächsten Reihe
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

                // Skalierung interpolieren
                double progress = (newY - startY) / (endY - startY);
                double currentScale = scaleStart + (scaleEnd - scaleStart) * progress;
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;

                await Task.Delay(16);
            }

            scaleTransform.ScaleX = scaleEnd;
            scaleTransform.ScaleY = scaleEnd;

            Stewardess.Source = new BitmapImage(new Uri(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Images", "stfront.png"), UriKind.Absolute));
        }


    }
}
