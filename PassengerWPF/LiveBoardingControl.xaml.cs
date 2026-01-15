using System;
using System.Collections.Generic;
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
            { "1A", (535, 285) }, { "1B", (557, 285) }, { "1C", (579, 285) },
            { "1D", (619, 285) }, { "1E", (640, 285) }, { "1F", (661, 285) },
            { "2A", (535, 321) }, { "2B", (557, 321) }, { "2C", (579, 321) },
            { "2D", (619, 321) }, { "2E", (640, 321) }, { "2F", (661, 321) },
            { "3A", (535, 353) }, { "3B", (557, 353) }, { "3C", (579, 353) },
            { "3D", (619, 353) }, { "3E", (640, 353) }, { "3F", (661, 353) },
            { "4A", (535, 386) }, { "4B", (557, 386) }, { "4C", (579, 386) },
            { "4D", (619, 386) }, { "4E", (640, 386) }, { "4F", (661, 386) },
            { "5A", (535, 421) }, { "5B", (557, 421) }, { "5C", (579, 421) },
            { "5D", (619, 421) }, { "5E", (640, 421) }, { "5F", (661, 421) },
            { "6A", (535, 453) }, { "6B", (557, 453) }, { "6C", (579, 453) },
            { "6D", (619, 453) }, { "6E", (640, 453) }, { "6F", (661, 453) },
            { "7A", (535, 487) }, { "7B", (557, 487) }, { "7C", (579, 487) },
            { "7D", (619, 487) }, { "7E", (640, 487) }, { "7F", (661, 487) },
            { "8A", (535, 520) }, { "8B", (557, 520) }, { "8C", (579, 520) },
            { "8D", (619, 520) }, { "8E", (640, 520) }, { "8F", (661, 520) },
            { "9A", (535, 554) }, { "9B", (557, 554) }, { "9C", (579, 554) },
            { "9D", (619, 554) }, { "9E", (640, 554) }, { "9F", (661, 554) },
            { "10A", (535, 589) }, { "10B", (557, 589) }, { "10C", (579, 589) },
            { "10D", (619, 589) }, { "10E", (640, 589) }, { "10F", (661, 589) },
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

        public LiveBoardingControl()
        {
            InitializeComponent();
            DataContext = this;

            LoadConfig();
            LoadSeatmap();
            CreateSeatMarkers();
            PositionSeatMarkers();

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += Timer_Tick;

            Loaded += (s, e) =>
            {
                SeatCanvas.UpdateLayout();
                RenderSeatmapScreenshot();
                timer.Start();
            };
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
                var rect = new Rectangle
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
                SeatCanvas.Children.Add(rect);
                seatMarkers[kvp.Key] = rect;
            }
        }

        private void PositionSeatMarkers()
        {
            foreach (var kvp in seatCoordinates)
            {
                if (seatMarkers.TryGetValue(kvp.Key, out var rect))
                {
                    Canvas.SetLeft(rect, kvp.Value.x);
                    Canvas.SetTop(rect, kvp.Value.y);
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!File.Exists(passengerDataPath)) return;

            // 1. CSV frisch einlesen
            var allSeats = seatCoordinates.Keys.ToList();
            var passengers = PassengerDataService.LoadPassengers(passengerDataPath, avatarsPath, allSeats);

            // 2. Neue Passagiere erkennen, die noch nicht im PassengerStore sind
            var newPassengers = passengers
                .Where(p => !PassengerStore.Passengers.Any(vm => vm.Name.Trim() == p.Name.Trim()))
                .ToList();

            foreach (var p in newPassengers)
            {
                // 3. Sitzplatz vergeben, falls leer
                if (string.IsNullOrWhiteSpace(p.Sitzplatz))
                {
                    var seat = FindNextFreeSeat(passengers);
                    if (seat != null)
                    {
                        p.Sitzplatz = seat;
                        UpdatePassengerSeatInCsv(p.Name, seat);
                    }
                }

                // 4. Passagier in UI + Sound
                AddPassengerToUI(p);
            }

            // 5. Liste aktualisieren
            UpdatePassengerListUI();
        }





        private string FindNextFreeSeat(List<Passenger> passengers)
        {
            var used = passengers.Where(p => !string.IsNullOrWhiteSpace(p.Sitzplatz))
                                 .Select(p => p.Sitzplatz)
                                 .ToHashSet();
            return seatCoordinates.Keys.FirstOrDefault(s => !used.Contains(s));
        }

        private void UpdatePassengerSeatInCsv(string name, string seat)
        {
            var lines = File.ReadAllLines(passengerDataPath).ToList();
            for (int i = 1; i < lines.Count; i++)
            {
                var cols = lines[i].Split(',');
                if (cols.Length == 0) continue;
                if (cols[0] == name)
                {
                    if (cols.Length > 1) cols[1] = seat;
                    else if (cols.Length == 1) cols = new string[] { name, seat };
                    lines[i] = string.Join(",", cols);
                    break;
                }
            }
            File.WriteAllLines(passengerDataPath, lines);
        }

        private void AddPassengerToUI(Passenger p)
        {
            try
            {
                if (!string.IsNullOrEmpty(boardingSoundPath) && File.Exists(boardingSoundPath))
                    new SoundPlayer(boardingSoundPath).Play();
            }
            catch { }

            if (seatMarkers.TryGetValue(p.Sitzplatz, out var rect))
            {
                rect.Fill = Brushes.Crimson;
                rect.ToolTip = p.Name;
            }

            PassengerStore.AddPassenger(new PassengerViewModel
            {
                Name = p.Name,
                Sitzplatz = p.Sitzplatz,
                Avatar = p.AvatarFile,
                Order1 = p.Order1,
                Order2 = p.Order2,
                Order3 = p.Order3,
                Order4 = p.Order4
            });

            RenderSeatmapScreenshot();
        }

        private void UpdatePassengerListUI()
        {
            BoardingListPanel.Children.Clear();
            foreach (var p in PassengerStore.Passengers)
            {
                BoardingListPanel.Children.Add(new TextBlock
                {
                    Text = $"{p.Name} ({p.Sitzplatz})",
                    Foreground = Brushes.White
                });
            }
        }

        private void RenderSeatmapScreenshot()
        {
            try
            {
                double targetWidth = 400;
                double targetHeight = 900;

                var rtb = new RenderTargetBitmap((int)targetWidth, (int)targetHeight, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();

                using (var dc = dv.RenderOpen())
                {
                    if (SeatmapImage.Source is BitmapSource bmp)
                    {
                        // Crop: nur oberer Teil entsprechend targetHeight
                        int cropHeightPx = (int)(bmp.PixelHeight * (targetHeight / SeatCanvas.Height));
                        if (cropHeightPx > bmp.PixelHeight) cropHeightPx = bmp.PixelHeight;

                        var cropped = new CroppedBitmap(bmp, new Int32Rect(0, 0, bmp.PixelWidth, cropHeightPx));
                        dc.DrawImage(cropped, new Rect(0, 0, targetWidth, targetHeight));
                    }

                    // Sitzrechtecke zeichnen mit Offset & Skalierung
                    foreach (var kvp in seatMarkers)
                    {
                        if (!seatCoordinates.TryGetValue(kvp.Key, out var coords)) continue;

                        double x = (coords.x * seatScaleX) + seatOffsetX;
                        double y = (coords.y * seatScaleY) + seatOffsetY;

                        // Nur sichtbar innerhalb Zielhöhe
                        if (y + kvp.Value.Height < 0 || y > targetHeight) continue;

                        double w = kvp.Value.Width * seatScaleX;
                        double h = kvp.Value.Height * seatScaleY;

                        dc.DrawRectangle(kvp.Value.Fill, null, new Rect(x, y, w, h));
                    }
                }

                rtb.Render(dv);

                // Datei ins Root neben der EXE
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempPath = IOPath.Combine(baseDir, "boarding_render_tmp.png");
                string finalPath = IOPath.Combine(baseDir, "boarding_render.png");

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





        // NEUE FUNKTION: Lädt alle Passagiere, egal ob Avatar oder Orders fehlen
        private List<Passenger> LoadPassengers(string csvPath, string avatarsPath)
        {
            var list = new List<Passenger>();
            if (!File.Exists(csvPath)) return list;

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return list;

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');

                if (cols.Length == 0) continue;

                string name = cols[0].Trim();
                if (string.IsNullOrEmpty(name)) continue;

                string seat = cols.Length > 1 ? cols[1].Trim() : "";
                string avatarFile = (cols.Length > 2 && !string.IsNullOrEmpty(cols[2].Trim()))
                                    ? IOPath.Combine(avatarsPath, cols[2].Trim())
                                    : "";

                string order1 = cols.Length > 3 ? cols[3].Trim() : "";
                string order2 = cols.Length > 4 ? cols[4].Trim() : "";
                string order3 = cols.Length > 5 ? cols[5].Trim() : "";
                string order4 = cols.Length > 6 ? cols[6].Trim() : "";

                list.Add(new Passenger
                {
                    Name = name,
                    Sitzplatz = seat,
                    AvatarFile = avatarFile,
                    Order1 = order1,
                    Order2 = order2,
                    Order3 = order3,
                    Order4 = order4
                });
            }

            return list;
        }
    }
}
