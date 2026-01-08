using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using IOPath = System.IO.Path;

namespace PassengerWPF
{
    public partial class LiveBoardingControl : UserControl
    {
        private readonly Dictionary<string, Rectangle> seatMarkers = new();
        private DispatcherTimer timer;

        private readonly Dictionary<string, (double x, double y)> seatCoordinates = new()
         {
      // Reihe 1
      { "1A", (535, 285) }, { "1B", (557, 285) }, { "1C", (579, 285) },
      { "1D", (619, 285) }, { "1E", (640, 285) }, { "1F", (661, 285) },

      // Reihe 2
      { "2A", (535, 321) }, { "2B", (557, 321) }, { "2C", (579, 321) },
      { "2D", (619, 321) }, { "2E", (640, 321) }, { "2F", (661, 321) },

      // Reihe 3
      { "3A", (535, 353) }, { "3B", (557, 353) }, { "3C", (579, 353) },
      { "3D", (619, 353) }, { "3E", (640, 353) }, { "3F", (661, 353) },

      // Reihe 4
      { "4A", (535, 386) }, { "4B", (557, 386) }, { "4C", (579, 386) },
      { "4D", (619, 386) }, { "4E", (640, 386) }, { "4F", (661, 386) },

      // Reihe 5
      { "5A", (535, 421) }, { "5B", (557, 421) }, { "5C", (579, 421) },
      { "5D", (619, 421) }, { "5E", (640, 421) }, { "5F", (661, 421) },

      // Reihe 6
      { "6A", (535, 453) }, { "6B", (557, 453) }, { "6C", (579, 453) },
      { "6D", (619, 453) }, { "6E", (640, 453) }, { "6F", (661, 453) },

      // Reihe 7
      { "7A", (535, 487) }, { "7B", (557, 487) }, { "7C", (579, 487) },
      { "7D", (619, 487) }, { "7E", (640, 487) }, { "7F", (661, 487) },

      // Reihe 8
      { "8A", (535, 520) }, { "8B", (557, 520) }, { "8C", (579, 520) },
      { "8D", (619, 520) }, { "8E", (640, 520) }, { "8F", (661, 520) },

      // Reihe 9
      { "9A", (535, 554) }, { "9B", (557, 554) }, { "9C", (579, 554) },
      { "9D", (619, 554) }, { "9E", (640, 554) }, { "9F", (661, 554) },

      // Reihe 10
      { "10A", (535, 589) }, { "10B", (557, 589) }, { "10C", (579, 589) },
      { "10D", (619, 589) }, { "10E", (640, 589) }, { "10F", (661, 589) },

      // Reihe 11
      { "11A", (535, 622) }, { "11B", (557, 622) }, { "11C", (579, 622) },
      { "11D", (619, 622) }, { "11E", (640, 622) }, { "11F", (661, 622) },

      // Reihe 12
      { "12A", (535, 655) }, { "12B", (557, 655) }, { "12C", (579, 655) },
      { "12D", (619, 655) }, { "12E", (640, 655) }, { "12F", (661, 655) },

      // Reihe 13
      { "13A", (535, 689) }, { "13B", (557, 689) }, { "13C", (579, 689) },
      { "13D", (619, 689) }, { "13E", (640, 689) }, { "13F", (661, 689) },

      // Reihe 14
      { "14A", (535, 723) }, { "14B", (557, 723) }, { "14C", (579, 723) },
      { "14D", (619, 723) }, { "14E", (640, 723) }, { "14F", (661, 723) },

      // Reihe 15
      { "15A", (535, 757) }, { "15B", (557, 757) }, { "15C", (579, 757) },
      { "15D", (619, 757) }, { "15E", (640, 757) }, { "15F", (661, 757) },

      // Reihe 16
      { "16A", (535, 791) }, { "16B", (557, 791) }, { "16C", (579, 791) },
      { "16D", (619, 791) }, { "16E", (640, 791) }, { "16F", (661, 791) },

      // Reihe 17
      { "17A", (535, 824) }, { "17B", (557, 824) }, { "17C", (579, 824) },
      { "17D", (619, 824) }, { "17E", (640, 824) }, { "17F", (661, 824) },
  };

        private string seatmapPath;
        private string passengerDataPath;
        private string boardingSoundPath;
        private string avatarsPath;

        private double seatOffsetX = -418.5;
        private double seatOffsetY = 35;
        private double seatScaleX = 1.03;
        private double seatScaleY = 1;

        private string jsonPath => IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private Queue<Passenger> initialQueue;
        private bool initialDone = false;
        private List<Passenger> lastSnapshot = new();

        public LiveBoardingControl()
        {
            InitializeComponent();
            DataContext = this;

            LoadConfig();
            LoadSeatmap();
            CreateSeatMarkers();
            PositionSeatMarkers();

            var allSeats = seatCoordinates.Keys.ToList();
            lastSnapshot = PassengerDataService.LoadPassengers(passengerDataPath, avatarsPath, allSeats);
            initialQueue = new Queue<Passenger>(lastSnapshot);

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += Timer_Tick;

            // GUI komplett fertig? dann Screenshot schreiben
            this.Loaded += (s, e) =>
            {
                SeatCanvas.UpdateLayout();
                RenderSeatmapScreenshot(); // einmaliges Initialbild
                timer.Start();             // Timer erst danach starten
            };

            UpdatePassengerListUI();
        }



        private void LoadConfig()
        {
            if (!File.Exists(jsonPath)) return;

            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<BoardingConfig>(File.ReadAllText(jsonPath));
                seatmapPath = config.Paths?.BGImage;
                boardingSoundPath = config.Paths?.BoardingSound;
                avatarsPath = config.Paths?.Avatars;
                passengerDataPath = config.Csv?.PassengerData;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler in config.json: " + ex.Message);
            }
        }

        private void LoadSeatmap()
        {
            if (!string.IsNullOrEmpty(seatmapPath) && File.Exists(seatmapPath))
            {
                try
                {
                    // absolute Datei-URI erzeugen
                    var uri = new Uri(IOPath.GetFullPath(seatmapPath), UriKind.Absolute);
                    SeatmapImage.Source = new BitmapImage(uri);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Laden der Sitzplan-Bilddatei:\n" + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Sitzplan-Datei existiert nicht:\n" + seatmapPath);
            }
        }


        private void CreateSeatMarkers()
        {
            foreach (var kvp in seatCoordinates)
            {
                var seatMarker = new Rectangle
                {
                    Width = 12,
                    Height = 15,
                    Fill = Brushes.LightGray,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 5,
                    ToolTip = kvp.Key
                };

                SeatCanvas.Children.Add(seatMarker);
                seatMarkers[kvp.Key] = seatMarker;
            }
        }

        private void PositionSeatMarkers()
        {
            foreach (var kvp in seatCoordinates)
            {
                if (!seatMarkers.TryGetValue(kvp.Key, out var rect)) continue;
                Canvas.SetLeft(rect, kvp.Value.x);
                Canvas.SetTop(rect, kvp.Value.y);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!initialDone)
            {
                if (initialQueue.Count > 0)
                {
                    var next = initialQueue.Dequeue();
                    AddPassengerToUI(next);
                }
                else
                {
                    initialDone = true;
                }

                UpdatePassengerListUI();
                return;
            }

            var allSeats = seatCoordinates.Keys.ToList();
            var nowList = PassengerDataService.LoadPassengers(passengerDataPath, avatarsPath, allSeats);

            var newOnes = nowList
                .Where(p => !lastSnapshot.Any(x => x.Name == p.Name && x.Sitzplatz == p.Sitzplatz))
                .ToList();

            lastSnapshot = nowList;

            foreach (var p in newOnes)
            {
                AddPassengerToUI(p);
            }

            UpdatePassengerListUI();
        }

        private void AddPassengerToUI(Passenger p)
        {
            try
            {
                if (!string.IsNullOrEmpty(boardingSoundPath) && File.Exists(boardingSoundPath))
                    new SoundPlayer(boardingSoundPath).Play();
            }
            catch { }

            if (!string.IsNullOrEmpty(p.Sitzplatz) && seatMarkers.ContainsKey(p.Sitzplatz))
            {
                seatMarkers[p.Sitzplatz].Fill = new SolidColorBrush(Color.FromRgb(220, 20, 60));
                seatMarkers[p.Sitzplatz].ToolTip = p.Name;
            }

            var vm = new PassengerViewModel
            {
                Name = p.Name,
                Sitzplatz = p.Sitzplatz,
                Avatar = p.AvatarFile,
                Order1 = p.Order1,
                Order2 = p.Order2,
                Order3 = p.Order3,
                Order4 = p.Order4
            };

            // **Nur noch PassengerStore verwenden**
            PassengerStore.AddPassenger(vm);

            RenderSeatmapScreenshot();
        }

        private void UpdatePassengerListUI()
        {
            BoardingListPanel.Children.Clear();

            // Items direkt aus PassengerStore
            foreach (var p in PassengerStore.Passengers)
            {
                var tb = new TextBlock
                {
                    Text = $"{p.Name}  ({p.Sitzplatz})",
                    Foreground = Brushes.White,
                    Margin = new Thickness(2, 1, 2, 1)
                };
                BoardingListPanel.Children.Add(tb);
            }
        }

        private void RenderSeatmapScreenshot()
        {
            try
            {
                double sourceWidth = SeatCanvas.ActualWidth;
                double sourceHeight = SeatCanvas.ActualHeight;

                if (sourceWidth <= 0 || sourceHeight <= 0) return;

                double targetWidth = 400;
                double targetHeight = 900;

                var rtb = new RenderTargetBitmap((int)targetWidth, (int)targetHeight, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();

                using (var dc = dv.RenderOpen())
                {
                    if (SeatmapImage.Source is BitmapSource bmp)
                    {
                        int cropHeightPx = (int)(bmp.PixelHeight * (targetHeight / sourceHeight));
                        var cropped = new CroppedBitmap(bmp, new Int32Rect(0, 0, bmp.PixelWidth, Math.Min(cropHeightPx, bmp.PixelHeight)));
                        dc.DrawImage(cropped, new Rect(0, 0, targetWidth, targetHeight));
                    }

                    foreach (var kvp in seatCoordinates)
                    {
                        if (!seatMarkers.TryGetValue(kvp.Key, out var rect)) continue;

                        double x = (kvp.Value.x * seatScaleX) + seatOffsetX;
                        double y = (kvp.Value.y * seatScaleY) + seatOffsetY;

                        if (y > targetHeight) continue;

                        double w = rect.Width * seatScaleX;
                        double h = rect.Height * seatScaleY;

                        dc.DrawRectangle(rect.Fill, null, new Rect(x, y, w, h));
                    }
                }

                rtb.Render(dv);

                // Stuff-Ordner im App-Verzeichnis
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string stuffDir = IOPath.Combine(baseDir, "stuff");
                Directory.CreateDirectory(stuffDir);

                string tempPath = IOPath.Combine(stuffDir, "boarding_render_tmp.png");
                string finalPath = IOPath.Combine(stuffDir, "boarding_render.png");

                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    encoder.Save(fs);
                }

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempPath, finalPath);

                PassengerStore.NotifySeatMapUpdated(finalPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Screenshot:\n" + ex.Message);
            }
        }



    }
}