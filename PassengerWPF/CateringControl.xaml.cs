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

        private double stewardessStartY = 800;       // Ausgangsposition unten
        private double stewardessStartScale = 5.0;   // Ausgangsgröße unten
        private double stewardessTargetScale = 2.7;  // Größe oben

        public CateringControl()
        {
            InitializeComponent();

            // Sitzkoordinaten
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
            PlaceDummyPassengers();
        }

        private void SetupBackgroundAndStewardess()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            // Hintergrund
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

            // Stewardess am unteren Rand
            Stewardess.Source = LoadStewardessImage("stfront.png");
            Stewardess.Width = 100;
            Stewardess.Height = 100;
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(stewardessStartScale, stewardessStartScale);
            Canvas.SetLeft(Stewardess, 620);
            Canvas.SetTop(Stewardess, stewardessStartY);
            Panel.SetZIndex(Stewardess, 5);
        }

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path)) return null;
            return new BitmapImage(new Uri(path, UriKind.Absolute));
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
                        Source = new BitmapImage(new Uri(path, UriKind.Absolute)) { CacheOption = BitmapCacheOption.OnLoad },
                        Width = size,
                        Height = size,
                        Tag = $"dummy_{row}_{seat}"
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

            // 1. Hochlaufen (hin)
            Stewardess.Source = LoadStewardessImage("stheck.png");
            await MoveStewardessVerticalWithScale(stewardessStartY, baseRowY["back"] - 45, stewardessStartScale, stewardessTargetScale);

            // 2. Bild wechseln wieder nach vorne
            Stewardess.Source = LoadStewardessImage("stfront.png");

            // 3. Rückweg (runter) entlang exakt derselben Y-Positionen
            await MoveStewardessVerticalWithScale(baseRowY["back"] - 45, stewardessStartY, stewardessTargetScale, stewardessStartScale);

            StartServiceButton.IsEnabled = true;
        }

        private async Task MoveStewardessVerticalWithScale(double startY, double targetY, double startScale, double targetScale)
        {
            var transform = Stewardess.RenderTransform as ScaleTransform;

            double speed = 3;
            int direction = targetY > startY ? 1 : -1;

            while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 0.5)
            {
                double currentY = Canvas.GetTop(Stewardess);
                double newY = currentY + direction * speed;
                if ((direction > 0 && newY > targetY) || (direction < 0 && newY < targetY))
                    newY = targetY;

                Canvas.SetTop(Stewardess, newY);

                // Linear skalieren
                double progress = Math.Abs(newY - startY) / Math.Abs(targetY - startY);
                double scale = startScale + (targetScale - startScale) * progress;
                transform.ScaleX = scale;
                transform.ScaleY = scale;

                await Task.Delay(16);
            }

            Canvas.SetTop(Stewardess, targetY);
            transform.ScaleX = targetScale;
            transform.ScaleY = targetScale;
        }
    }
}
