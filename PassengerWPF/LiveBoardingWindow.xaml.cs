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
            { "1A", (0.322, 0.20) }, { "1B", (0.38, 0.20) }, { "1C", (0.44, 0.20) },
            { "1D", (0.54, 0.20) }, { "1E", (0.592, 0.20) }, { "1F", (0.645, 0.20) },
            { "2A", (0.322, 0.225) }, { "2B", (0.38, 0.225) }, { "2C", (0.44, 0.225) },
            { "2D", (0.54, 0.225) }, { "2E", (0.592, 0.225) }, { "2F", (0.645, 0.225) },
            // Restliche Reihen 3–17 folgen...
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
