using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PassengerWPF
{
    public partial class LiveBoardingWindow : Window
    {
        private readonly Dictionary<string, Rectangle> seatMarkers = new();
        private DispatcherTimer timer;

        // Sitz-Koordinaten relativ (0..1)
        private readonly Dictionary<string, (double x, double y)> seatCoordinates = new()
        {
            { "1A", (0.350, 0.208) }, { "1B", (0.408, 0.208) }, { "1C", (0.468, 0.208) },
            { "1D", (0.568, 0.208) }, { "1E", (0.620, 0.208) }, { "1F", (0.673, 0.208) },
            { "2A", (0.350, 0.233) }, { "2B", (0.408, 0.233) }, { "2C", (0.468, 0.233) },
            { "2D", (0.568, 0.233) }, { "2E", (0.620, 0.233) }, { "2F", (0.673, 0.233) },
            { "3A", (0.350, 0.256) }, { "3B", (0.408, 0.256) }, { "3C", (0.468, 0.256) },
            { "3D", (0.568, 0.256) }, { "3E", (0.620, 0.256) }, { "3F", (0.673, 0.256) },
            { "4A", (0.350, 0.281) }, { "4B", (0.408, 0.281) }, { "4C", (0.468, 0.281) },
            { "4D", (0.568, 0.281) }, { "4E", (0.620, 0.281) }, { "4F", (0.673, 0.281) },
            { "5A", (0.350, 0.3047) }, { "5B", (0.408, 0.3047) }, { "5C", (0.468, 0.3047) },
            { "5D", (0.568, 0.3047) }, { "5E", (0.620, 0.3047) }, { "5F", (0.673, 0.3047) },
            { "6A", (0.350, 0.3297) }, { "6B", (0.408, 0.3297) }, { "6C", (0.468, 0.3297) },
            { "6D", (0.568, 0.3297) }, { "6E", (0.620, 0.3297) }, { "6F", (0.673, 0.3297) },
            { "7A", (0.350, 0.355) }, { "7B", (0.408, 0.355) }, { "7C", (0.468, 0.355) },
            { "7D", (0.568, 0.355) }, { "7E", (0.620, 0.355) }, { "7F", (0.673, 0.355) },
            { "8A", (0.350, 0.3785) }, { "8B", (0.408, 0.3785) }, { "8C", (0.468, 0.3785) },
            { "8D", (0.568, 0.3785) }, { "8E", (0.620, 0.3785) }, { "8F", (0.673, 0.3785) },
            { "9A", (0.350, 0.401) }, { "9B", (0.408, 0.401) }, { "9C", (0.468, 0.401) },
            { "9D", (0.568, 0.401) }, { "9E", (0.620, 0.401) }, { "9F", (0.673, 0.401) },
            { "10A", (0.350, 0.425) }, { "10B", (0.408, 0.425) }, { "10C", (0.468, 0.425) },
            { "10D", (0.568, 0.425) }, { "10E", (0.620, 0.425) }, { "10F", (0.673, 0.425) },
            { "11A", (0.350, 0.449) }, { "11B", (0.408, 0.449) }, { "11C", (0.468, 0.449) },
            { "11D", (0.568, 0.449) }, { "11E", (0.620, 0.449) }, { "11F", (0.673, 0.449) },
            { "12A", (0.350, 0.473) }, { "12B", (0.408, 0.473) }, { "12C", (0.468, 0.473) },
            { "12D", (0.568, 0.473) }, { "12E", (0.620, 0.473) }, { "12F", (0.673, 0.473) },
            { "13A", (0.350, 0.500) }, { "13B", (0.408, 0.500) }, { "13C", (0.468, 0.500) },
            { "13D", (0.568, 0.500) }, { "13E", (0.620, 0.500) }, { "13F", (0.673, 0.500) },
            { "14A", (0.350, 0.523) }, { "14B", (0.408, 0.523) }, { "14C", (0.468, 0.523) },
            { "14D", (0.568, 0.523) }, { "14E", (0.620, 0.523) }, { "14F", (0.673, 0.523) },
            { "15A", (0.350, 0.545) }, { "15B", (0.408, 0.545) }, { "15C", (0.468, 0.545) },
            { "15D", (0.568, 0.545) }, { "15E", (0.620, 0.545) }, { "15F", (0.673, 0.545) },
            { "16A", (0.350, 0.569) }, { "16B", (0.408, 0.569) }, { "16C", (0.468, 0.569) },
            { "16D", (0.568, 0.569) }, { "16E", (0.620, 0.569) }, { "16F", (0.673, 0.569) },
            { "17A", (0.350, 0.593) }, { "17B", (0.408, 0.593) }, { "17C", (0.468, 0.593) },
            { "17D", (0.568, 0.593) }, { "17E", (0.620, 0.593) }, { "17F", (0.673, 0.593) },
        };

        private string seatmapPath;
        private string actualFlightPath;
        private string boardingSoundPath;

        private string jsonPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public LiveBoardingWindow()
        {
            InitializeComponent();
            LoadConfig();
            LoadSeatmap();

            // Marker erstellen
            CreateSeatMarkers();

            // Marker positionieren, sobald das Bild geladen ist
            SeatmapImage.Loaded += (s, e) => PositionSeatMarkers();
            SeatmapImage.SizeChanged += (s, e) => PositionSeatMarkers();

            LoadBoardingList();
            InitTimer();
        }

        private void LoadConfig()
        {
            if (!File.Exists(jsonPath)) return;

            try
            {
                var config = JsonSerializer.Deserialize<BoardingConfig>(File.ReadAllText(jsonPath));
                if (config != null)
                {
                    seatmapPath = config.Paths?.BGImage;
                    actualFlightPath = config.Csv?.ActualFlight;
                    boardingSoundPath = config.Paths?.BoardingSound;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Konfigurationsdatei: {ex.Message}");
            }
        }

        private void LoadSeatmap()
        {
            if (!string.IsNullOrEmpty(seatmapPath) && File.Exists(seatmapPath))
            {
                SeatmapImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(seatmapPath));
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
                    Fill = System.Windows.Media.Brushes.LightGray,
                    Stroke = System.Windows.Media.Brushes.DarkGray,
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 5,
                    ToolTip = ""
                };

                seatMarker.MouseEnter += (s, e) => seatMarker.Stroke = System.Windows.Media.Brushes.Yellow;
                seatMarker.MouseLeave += (s, e) => seatMarker.Stroke = System.Windows.Media.Brushes.DarkGray;

                SeatCanvas.Children.Add(seatMarker);
                seatMarkers[kvp.Key] = seatMarker;
            }
        }

        private void PositionSeatMarkers()
        {
            if (SeatmapImage.Source == null) return;

            var bitmap = (System.Windows.Media.Imaging.BitmapSource)SeatmapImage.Source;
            double canvasWidth = SeatCanvas.ActualWidth;
            double canvasHeight = SeatCanvas.ActualHeight;

            // Canvas muss reale Größe haben
            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double ratio = Math.Min(canvasWidth / bitmap.PixelWidth, canvasHeight / bitmap.PixelHeight);
            double offsetX = (canvasWidth - bitmap.PixelWidth * ratio) / 2;
            double offsetY = (canvasHeight - bitmap.PixelHeight * ratio) / 2;

            foreach (var kvp in seatCoordinates)
            {
                if (!seatMarkers.TryGetValue(kvp.Key, out var rect)) continue;
                var (relX, relY) = kvp.Value;

                double x = offsetX + relX * bitmap.PixelWidth * ratio;
                double y = offsetY + relY * bitmap.PixelHeight * ratio;

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
            }
        }

        private void LoadBoardingList()
        {
            if (string.IsNullOrEmpty(actualFlightPath) || !File.Exists(actualFlightPath)) return;

            BoardingListPanel.Children.Clear();

            var lines = File.ReadAllLines(actualFlightPath).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string name = parts[0];
                string seat = parts[1].ToUpper();

                BoardingListPanel.Children.Add(new TextBlock
                {
                    Text = $"{name} → {seat}",
                    Margin = new Thickness(5),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold
                });

                if (seatMarkers.ContainsKey(seat))
                {
                    seatMarkers[seat].Fill = System.Windows.Media.Brushes.LawnGreen;
                    seatMarkers[seat].ToolTip = name;
                }
            }
        }

        private void InitTimer()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                if (!string.IsNullOrEmpty(boardingSoundPath) && File.Exists(boardingSoundPath))
                {
                    new SoundPlayer(boardingSoundPath).Play();
                }
            };
            timer.Start();
        }
    }

    public class BoardingConfig
    {
        [JsonPropertyName("Paths")] public BoardingPaths Paths { get; set; }
        [JsonPropertyName("Csv")] public BoardingCsv Csv { get; set; }
    }

    public class BoardingPaths
    {
        [JsonPropertyName("BGImage")] public string BGImage { get; set; }
        [JsonPropertyName("BoardingSound")] public string BoardingSound { get; set; }
    }

    public class BoardingCsv
    {
        [JsonPropertyName("ActualFlight")] public string ActualFlight { get; set; }
    }
}
