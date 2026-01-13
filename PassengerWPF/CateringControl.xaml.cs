using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PassengerWPF
{
    public partial class CateringControl : UserControl
    {
        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;
        private readonly Dictionary<string, double> rowGangOffset;

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

            // aus alter Testfunktion abgeleitet
            rowGangOffset = new()
            {
                ["front"] = 60,
                ["near"] = 50,
                ["back"] = 40
            };

            SetupBackgroundAndStewardess();
            PlaceDummyPassengers();
        }

        private void SetupBackgroundAndStewardess()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            string bgPath = Path.Combine(cabinDir, "seatsbackground.png");
            if (File.Exists(bgPath))
            {
                var bg = new Image
                {
                    Source = new BitmapImage(new Uri(bgPath, UriKind.Absolute)),
                    Width = 1355,
                    Height = 768
                };
                Canvas.SetLeft(bg, 0);
                Canvas.SetTop(bg, 0);
                Panel.SetZIndex(bg, 1);
                CabinCanvas.Children.Add(bg);
            }

            Stewardess.Source = LoadStewardessImage("stfront.png");
            Stewardess.Width = 100;
            Stewardess.Height = 100;
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(5.0, 5.0);

            Canvas.SetLeft(Stewardess, 620);
            Canvas.SetTop(Stewardess, 800); // sichtbar, unten im Gang
            Panel.SetZIndex(Stewardess, 5);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            return File.Exists(path) ? new BitmapImage(new Uri(path, UriKind.Absolute)) : null;
        }

        private void PlaceDummyPassengers()
        {
            string avatarsPath = ConfigService.Current.Paths.Avatars;
            string avatarFile = "avatar1.png";

            foreach (var row in seatPositions.Keys)
            {
                foreach (var seat in seatPositions[row].Keys)
                {
                    string path = Path.Combine(avatarsPath, avatarFile);
                    if (!File.Exists(path)) continue;

                    double x = seatPositions[row][seat];
                    double y = baseRowY[row];
                    double size = avatarSize[row];

                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(path, UriKind.Absolute)),
                        Width = size,
                        Height = size
                    };

                    Canvas.SetLeft(img, x - size / 2);
                    Canvas.SetTop(img, y);
                    Panel.SetZIndex(img, row switch
                    {
                        "back" => 2,
                        "near" => 5,
                        "front" => 8,
                        _ => 5
                    });

                    CabinCanvas.Children.Add(img);
                }
            }
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            StartServiceButton.IsEnabled = false;

            Stewardess.Source = LoadStewardessImage("stheck.png");

            double startY = Canvas.GetTop(Stewardess);
            double targetY = baseRowY["back"] + rowGangOffset["back"];

            double startScale = (Stewardess.RenderTransform as ScaleTransform)?.ScaleX ?? 5.0;
            double targetScale = 2.7;

            await MoveStewardessVerticalWithScale(startY, targetY, startScale, targetScale);

            StartServiceButton.IsEnabled = true;
        }

        private async Task MoveStewardessVerticalWithScale(
            double startY,
            double targetY,
            double startScale,
            double targetScale)
        {
            if (Stewardess.RenderTransform is not ScaleTransform transform)
                return;

            double speed = 3;
            double totalDistance = Math.Abs(targetY - startY);

            while (true)
            {
                double currentY = Canvas.GetTop(Stewardess);
                double delta = Math.Sign(targetY - currentY) * speed;
                double newY = currentY + delta;

                if ((delta < 0 && newY <= targetY) || (delta > 0 && newY >= targetY))
                    newY = targetY;

                Canvas.SetTop(Stewardess, newY);

                double progress = Math.Abs(newY - startY) / totalDistance;
                double scale = startScale + (targetScale - startScale) * progress;

                transform.ScaleX = scale;
                transform.ScaleY = scale;

                if (newY == targetY)
                    break;

                await Task.Delay(16);
            }
        }
    }
}
