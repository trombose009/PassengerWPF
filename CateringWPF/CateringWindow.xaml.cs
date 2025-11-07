using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CateringWPF
{
    public partial class CateringWindow : Window
    {
        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;
        private readonly HashSet<string> occupiedSeats = new();
        private readonly string cateringCsvPath;
        private List<Passenger> passengers = new();

        public CateringWindow()
        {
            InitializeComponent();

            cateringCsvPath = ConfigService.Current.Csv.Catering;

            // Sitz-Logik
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
            LoadPassengersFromCsv();
            AssignSeatsToFreePassengers();
            PlacePassengers();
        }

        #region CSV Laden / Speichern

        private void LoadPassengersFromCsv()
        {
            passengers.Clear();
            occupiedSeats.Clear();

            if (!File.Exists(cateringCsvPath)) return;

            var lines = File.ReadAllLines(cateringCsvPath).Skip(1);
            string avatarDir = ConfigService.Current.Paths.Avatars;

            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                string name = parts[0].Trim('"');
                string seat = parts[1].Trim('"');
                string avatarFile = parts[2];

                if (string.IsNullOrEmpty(avatarFile) || !File.Exists(System.IO.Path.Combine(avatarDir, avatarFile)))
                    avatarFile = "placeholder.png";

                passengers.Add(new Passenger
                {
                    Name = name,
                    Seat = string.IsNullOrWhiteSpace(seat) ? null : seat,
                    AvatarFile = avatarFile
                });

                if (!string.IsNullOrWhiteSpace(seat))
                    occupiedSeats.Add(seat);
            }
        }

        private void SavePassengersToCsv()
        {
            if (passengers.Count == 0) return;

            using var writer = new StreamWriter(cateringCsvPath);
            writer.WriteLine("Name,Sitzplatz,Avatar,orders1,orders2,orders3,orders4");

            foreach (var p in passengers)
            {
                writer.WriteLine($"\"{p.Name}\",\"{p.Seat}\",{p.AvatarFile},{string.Join(",", p.Orders)}");
            }
        }

        #endregion

        #region Sitzplatz Zuweisung

        private void AssignSeatsToFreePassengers()
        {
            foreach (var p in passengers.Where(p => string.IsNullOrEmpty(p.Seat)))
            {
                string assignedSeat = null;

                // Fensterplätze bevorzugt
                foreach (var row in seatPositions.Keys)
                {
                    foreach (var seat in new[] { "A", "F", "B", "C", "D", "E" })
                    {
                        if (!seatPositions[row].ContainsKey(seat)) continue;
                        string seatId = $"{rowPositionsNumber(row)}{seat}";
                        if (!occupiedSeats.Contains(seatId))
                        {
                            assignedSeat = seatId;
                            break;
                        }
                    }
                    if (assignedSeat != null) break;
                }

                if (assignedSeat != null)
                {
                    p.Seat = assignedSeat;
                    occupiedSeats.Add(assignedSeat);
                }
            }

            SavePassengersToCsv();
        }

        private int rowPositionsNumber(string row)
        {
            return row switch
            {
                "front" => 1,
                "near" => 10,
                "back" => 20,
                _ => 10
            };
        }

        #endregion

        #region Platzierung

        private void SetupBackground()
        {
            string cabinDir = ConfigService.Current.Paths.Cabin;

            void AddImage(string fileName, int zIndex, bool hitTest = true)
            {
                var imgPath = System.IO.Path.Combine(cabinDir, fileName);
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
        }

        private void PlacePassengers()
        {
            // Nur Passagier-Avatare löschen, Stewardess und Hintergrund bleiben
            var passengerImgs = CabinCanvas.Children
                                           .OfType<Image>()
                                           .Where(img => img.Tag?.ToString() == "avatar_passenger")
                                           .ToList();

            foreach (var img in passengerImgs)
                CabinCanvas.Children.Remove(img);

            string avatarDir = ConfigService.Current.Paths.Avatars;

            foreach (var p in passengers)
            {
                // Skip, wenn Avatar bereits gezeichnet wurde
                if (p.AvatarImage != null && CabinCanvas.Children.Contains(p.AvatarImage))
                    continue;

                // Sitzplatz prüfen
                if (string.IsNullOrEmpty(p.Seat))
                    AssignSeatToPassenger(p);

                string rowType = DetermineRowFromSeat(p.Seat);
                string seatLetter = p.Seat.Last().ToString();

                if (!seatPositions[rowType].ContainsKey(seatLetter)) continue;

                double x = seatPositions[rowType][seatLetter];
                double y = baseRowY[rowType];
                double size = avatarSize[rowType];

                string imgPath = Path.Combine(avatarDir, p.AvatarFile);
                if (!File.Exists(imgPath))
                    imgPath = Path.Combine(avatarDir, "placeholder.png");

                var img = new Image
                {
                    Width = size,
                    Height = size,
                    Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute)),
                    Tag = "avatar_passenger"
                };

                Canvas.SetLeft(img, x - size / 2);
                Canvas.SetTop(img, y);
                Panel.SetZIndex(img, rowType == "back" ? 2 : rowType == "near" ? 5 : 7);
                CabinCanvas.Children.Add(img);

                p.AvatarImage = img;
                occupiedSeats.Add(p.Seat);
            }
        }

        private void AssignSeatToPassenger(Passenger p)
        {
            foreach (var row in seatPositions.Keys)
            {
                foreach (var seat in new[] { "A", "F", "B", "C", "D", "E" })
                {
                    if (!seatPositions[row].ContainsKey(seat)) continue;
                    string seatId = $"{rowPositionsNumber(row)}{seat}";
                    if (!occupiedSeats.Contains(seatId))
                    {
                        p.Seat = seatId;
                        occupiedSeats.Add(seatId);
                        return;
                    }
                }
            }
        }


        private string DetermineRowFromSeat(string seat)
        {
            if (string.IsNullOrEmpty(seat)) return "back";

            int rowNum;
            if (int.TryParse(new string(seat.TakeWhile(char.IsDigit).ToArray()), out rowNum))
            {
                if (rowNum <= 5) return "front";
                if (rowNum <= 15) return "near";
                return "back";
            }

            return "back";
        }

        #endregion

        #region Stewardess (unverändert)

        private BitmapImage LoadStewardessImage(string fileName)
        {
            string path = System.IO.Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path)) return null;
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }

        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Bewegungslogik unverändert
            await Task.CompletedTask;
        }

        #endregion

        #region Passenger Klasse

        private class Passenger
        {
            public string Name { get; set; }
            public string Seat { get; set; }
            public string AvatarFile { get; set; }
            public Image AvatarImage { get; set; }
            public string[] Orders { get; set; } = new string[4];
        }

        #endregion
    }
}
