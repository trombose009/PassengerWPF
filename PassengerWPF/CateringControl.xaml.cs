using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace PassengerWPF
{
    public partial class CateringControl : UserControl
    {
        private List<Passenger> passengers = new();

        private readonly Dictionary<string, Dictionary<string, double>> seatPositions;
        private readonly Dictionary<string, double> baseRowY;
        private readonly Dictionary<string, double> avatarSize;

        private readonly Dictionary<Passenger, int> servedOrders = new();
        private readonly Dictionary<Passenger, Border> orderBubbleRefs = new();
        private readonly Dictionary<Passenger, Image[]> orderIconRefs = new();

        private const double MiddleX = 620;
        private const double StartY = 800;

        private const double GangOffsetBack = 115;
        private const double GangOffsetNear = 125;
        private const double GangOffsetFront = 260;

        private string currentRowForZ = "front";

        // ✅ nullable -> keine NonNullable-Warnung
        private MediaPlayer? cateringMusicPlayer;
        private bool cateringMusicLoopWired = false;
        private bool cateringMusicIsPlaying = false;

        private const int CateringMusicStopDelayMs = 800;
        private const int CateringMusicFadeOutMs = 3500;

        private int movementSoundRefs = 0;
        private int musicFadeVersion = 0;

        private const double CateringMusicTargetVolume = 0.35;

        private bool _serviceRunning = false;

        private double GangY(string row) => row switch
        {
            "back" => baseRowY["back"] + GangOffsetBack,
            "near" => baseRowY["near"] + GangOffsetNear,
            "front" => baseRowY["front"] + GangOffsetFront,
            _ => StartY
        };

        private double RowScale(string row) => row switch
        {
            "front" => 5.0,
            "near" => 3.8,
            "back" => 2.7,
            _ => 5.0
        };

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

            SetupBackgroundAndStewardess();
            SetupCateringMusic();

            // ✅ KEIN Auto-Start mehr
            Loaded += async (_, __) =>
            {
                LoadAndPlacePassengers();
                await Task.Delay(200);
            };
        }

        // -----------------------------
        // Start Button
        // -----------------------------
        private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_serviceRunning) return;
            _serviceRunning = true;

            StartServiceButton.IsEnabled = false;
            StartServiceButton.Opacity = 0.4;

            try
            {
                await StartRouteSafeAsync();
            }
            finally
            {
                _serviceRunning = false;

                // Wenn du ihn nur 1x willst -> Collapsed setzen
                // StartServiceButton.Visibility = Visibility.Collapsed;

                StartServiceButton.IsEnabled = true;
                StartServiceButton.Opacity = 1.0;
            }
        }

        // -----------------------------
        // Stewardess helpers
        // -----------------------------
        private void SetStewardessImage(string fileName)
        {
            var img = LoadStewardessImage(fileName);
            if (img != null)
                Stewardess.Source = img;
        }

        private void SetupBackgroundAndStewardess()
        {
            // ✅ Canvas schwarz ist okay (betrifft nicht Avatar-Grids)
            CabinCanvas.Background = Brushes.Black;

            string cabinDir = ConfigService.Current.Paths.Cabin;

            void AddLayer(string fileName, int zIndex)
            {
                string path = Path.Combine(cabinDir, fileName);
                if (!File.Exists(path))
                {
                    Debug.WriteLine($"[LAYER] Missing: {path}");
                    return;
                }

                var img = new Image
                {
                    Source = LoadBitmapNoLock(path),
                    Width = 1355,
                    Height = 768,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(img, 0);
                Canvas.SetTop(img, 0);
                Panel.SetZIndex(img, zIndex);
                CabinCanvas.Children.Add(img);
            }

            // background (1) < st(back=3) < seatsmiddle (4) < st(near=6) < seatsfront (7) < st(front=9)
            AddLayer("seatsbackground.png", 1);
            AddLayer("seatsmiddle.png", 4);
            AddLayer("seatsfront.png", 7);

            Stewardess.Width = 100;
            Stewardess.Height = 100;
            Stewardess.RenderTransformOrigin = new Point(0.5, 1);
            Stewardess.RenderTransform = new ScaleTransform(RowScale("front"), RowScale("front"));

            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);

            currentRowForZ = "front";
            SetStewardessZIndex(currentRowForZ);

            SetStewardessImage("stfront.png");
        }

        private BitmapImage? LoadStewardessImage(string fileName)
        {
            string path = Path.Combine(ConfigService.Current.Paths.Stewardess, fileName);
            if (!File.Exists(path))
            {
                Debug.WriteLine($"[ST] Missing: {path}");
                return null;
            }
            return LoadBitmapNoLock(path);
        }

        private static BitmapImage LoadBitmapNoLock(string absolutePath)
        {
            var bmp = new BitmapImage();
            using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = fs;
                bmp.EndInit();
                bmp.Freeze();
            }
            return bmp;
        }

        // -----------------------------
        // Catering music
        // -----------------------------
        private void SetupCateringMusic()
        {
            try
            {
                string musicPath = ConfigService.Current.Paths.CateringMusic;

                if (string.IsNullOrWhiteSpace(musicPath))
                {
                    Debug.WriteLine("[MUSIC] CateringMusic path empty.");
                    return;
                }

                if (!Path.IsPathRooted(musicPath))
                    musicPath = PathHelpers.MakeAbsolute(musicPath);

                if (!File.Exists(musicPath))
                {
                    Debug.WriteLine($"[MUSIC] Missing: {musicPath}");
                    return;
                }

                cateringMusicPlayer = new MediaPlayer();
                cateringMusicPlayer.Open(new Uri(musicPath, UriKind.Absolute));
                cateringMusicPlayer.Volume = CateringMusicTargetVolume;

                if (!cateringMusicLoopWired)
                {
                    cateringMusicLoopWired = true;
                    cateringMusicPlayer.MediaEnded += (_, __) =>
                    {
                        try
                        {
                            if (cateringMusicPlayer == null) return;
                            cateringMusicPlayer.Position = TimeSpan.Zero;
                            cateringMusicPlayer.Play();
                        }
                        catch { }
                    };
                }

                Debug.WriteLine($"[MUSIC] Loaded: {musicPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MUSIC] EXCEPTION: " + ex);
            }
        }

        private void BeginMovementSound()
        {
            try
            {
                if (cateringMusicPlayer == null) return;

                musicFadeVersion++;
                movementSoundRefs++;

                if (movementSoundRefs == 1)
                {
                    cateringMusicPlayer.Volume = CateringMusicTargetVolume;

                    if (!cateringMusicIsPlaying)
                    {
                        cateringMusicPlayer.Play();
                        cateringMusicIsPlaying = true;
                    }
                    else
                    {
                        cateringMusicPlayer.Volume = CateringMusicTargetVolume;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MUSIC] Begin EXCEPTION: " + ex);
            }
        }

        private void EndMovementSound()
        {
            try
            {
                if (cateringMusicPlayer == null) return;

                movementSoundRefs--;
                if (movementSoundRefs <= 0)
                {
                    movementSoundRefs = 0;
                    int localVersion = ++musicFadeVersion;
                    _ = FadeOutAndStopAsync(localVersion, CateringMusicStopDelayMs, CateringMusicFadeOutMs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MUSIC] End EXCEPTION: " + ex);
            }
        }

        private async Task FadeOutAndStopAsync(int version, int stopDelayMs, int fadeMs)
        {
            try
            {
                if (cateringMusicPlayer == null) return;

                int waited = 0;
                while (waited < stopDelayMs)
                {
                    if (version != musicFadeVersion) return;
                    if (movementSoundRefs > 0) return;
                    await Task.Delay(50);
                    waited += 50;
                }

                if (cateringMusicPlayer == null) return;

                double startVol = cateringMusicPlayer.Volume;
                if (startVol <= 0.001)
                {
                    if (version == musicFadeVersion && movementSoundRefs == 0)
                    {
                        cateringMusicPlayer.Stop();
                        cateringMusicIsPlaying = false;
                        cateringMusicPlayer.Volume = CateringMusicTargetVolume;
                    }
                    return;
                }

                int steps = 40;
                int stepDelay = Math.Max(15, fadeMs / steps);

                for (int i = 1; i <= steps; i++)
                {
                    if (cateringMusicPlayer == null) return;
                    if (version != musicFadeVersion) { cateringMusicPlayer.Volume = CateringMusicTargetVolume; return; }
                    if (movementSoundRefs > 0) { cateringMusicPlayer.Volume = CateringMusicTargetVolume; return; }

                    double t = i / (double)steps;
                    cateringMusicPlayer.Volume = startVol * (1.0 - t);

                    await Task.Delay(stepDelay);
                }

                if (cateringMusicPlayer != null && version == musicFadeVersion && movementSoundRefs == 0)
                {
                    cateringMusicPlayer.Stop();
                    cateringMusicIsPlaying = false;
                    cateringMusicPlayer.Volume = CateringMusicTargetVolume;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MUSIC] FadeOut EXCEPTION: " + ex);
            }
        }

        // -----------------------------
        // Passengers (dein Code unverändert)
        // -----------------------------
        private void LoadAndPlacePassengers()
        {
            RemoveOldPassengerElements();

            string csvPath = ConfigService.Current.Csv.PassengerData;
            string avatarsPath = ConfigService.Current.Paths.Avatars;

            passengers = LoadPassengersFromCsv(csvPath).ToList();
            Debug.WriteLine($"[CSV] Loaded passengers: {passengers.Count}");

            servedOrders.Clear();
            orderBubbleRefs.Clear();
            orderIconRefs.Clear();
            foreach (var p in passengers)
                servedOrders[p] = 0;

            var takenSeatsPerRow = seatPositions.Keys.ToDictionary(k => k, _ => new HashSet<string>());

            const double BubbleSize = 90;
            const double BubbleGap = 20;

            foreach (var p in passengers)
            {
                p.Name = (p.Name ?? "").Trim();
                p.Sitzplatz = (p.Sitzplatz ?? "").Trim().ToUpper();
                p.AvatarFile = (p.AvatarFile ?? "").Trim();

                if (string.IsNullOrWhiteSpace(p.AvatarFile))
                    continue;

                string imgPath = Path.Combine(avatarsPath, p.AvatarFile);
                if (!File.Exists(imgPath))
                    continue;

                var (row, seatLetter) = GetRowAndSeatLetter(p.Sitzplatz);
                if (string.IsNullOrWhiteSpace(row) || !seatPositions.ContainsKey(row))
                    row = "near";

                if (!seatPositions[row].ContainsKey(seatLetter) || takenSeatsPerRow[row].Contains(seatLetter))
                {
                    var free = seatPositions[row].Keys.Except(takenSeatsPerRow[row]).ToList();
                    if (free.Count == 0) continue;
                    seatLetter = free[0];
                }

                takenSeatsPerRow[row].Add(seatLetter);

                double x = seatPositions[row][seatLetter];
                double y = baseRowY[row];
                double size = avatarSize[row];

                var avatarImg = new Image
                {
                    Width = size,
                    Height = size,
                    Source = LoadBitmapNoLock(imgPath),
                    Stretch = Stretch.UniformToFill,
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };

                var nameText = new TextBlock
                {
                    Text = p.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, -16, 0, 0),
                    IsHitTestVisible = false,
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 6,
                        ShadowDepth = 0,
                        Color = Colors.Black,
                        Opacity = 0.95
                    }
                };

                var bubble = CreateOrderBubbleHidden(p, row, out var icons);
                bubble.Width = BubbleSize;
                bubble.Height = BubbleSize;

                if (icons != null)
                {
                    foreach (var ic in icons)
                    {
                        if (ic == null) continue;
                        ic.HorizontalAlignment = HorizontalAlignment.Center;
                        ic.VerticalAlignment = VerticalAlignment.Center;
                        ic.Stretch = Stretch.Uniform;
                    }
                }

                orderBubbleRefs[p] = bubble;
                orderIconRefs[p] = icons;

                double containerW = BubbleSize;
                double containerH = BubbleSize + BubbleGap + size;

                var avatarHost = new Grid
                {
                    Width = containerW,
                    Height = size,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false,
                    Background = Brushes.Transparent // ✅ wichtig: niemals schwarz
                };
                avatarHost.Children.Add(avatarImg);
                avatarHost.Children.Add(nameText);

                var avatarContainer = new Grid
                {
                    Width = containerW,
                    Height = containerH,
                    Tag = $"avatar_passenger_{p.Name}",
                    IsHitTestVisible = false,
                    ClipToBounds = false,
                    Background = Brushes.Transparent // ✅ wichtig: niemals schwarz
                };

                bubble.HorizontalAlignment = HorizontalAlignment.Center;
                bubble.VerticalAlignment = VerticalAlignment.Top;

                avatarContainer.Children.Add(bubble);
                avatarContainer.Children.Add(avatarHost);

                double topOffset = containerH - size;

                Canvas.SetLeft(avatarContainer, x - containerW / 2.0);
                Canvas.SetTop(avatarContainer, y - topOffset);

                Panel.SetZIndex(avatarContainer, row switch
                {
                    "back" => 2,
                    "near" => 5,
                    "front" => 8,
                    _ => 5
                });

                CabinCanvas.Children.Add(avatarContainer);
            }
        }

        private void RemoveOldPassengerElements()
        {
            var old = CabinCanvas.Children
                .OfType<FrameworkElement>()
                .Where(c => c.Tag is string t && t.StartsWith("avatar_passenger_"))
                .ToList();

            foreach (var e in old)
                CabinCanvas.Children.Remove(e);
        }

        private IEnumerable<Passenger> LoadPassengersFromCsv(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Debug.WriteLine($"[CSV] Missing: {csvPath}");
                yield break;
            }

            var lines = File.ReadAllLines(csvPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (lines.Count < 2) yield break;

            for (int i = 1; i < lines.Count; i++)
            {
                var parts = lines[i].Split(',');

                yield return new Passenger
                {
                    Name = parts.ElementAtOrDefault(0)?.Trim() ?? "",
                    Sitzplatz = parts.ElementAtOrDefault(1)?.Trim() ?? "",
                    AvatarFile = parts.ElementAtOrDefault(2)?.Trim() ?? "",
                    Order1 = parts.ElementAtOrDefault(3)?.Trim() ?? "",
                    Order2 = parts.ElementAtOrDefault(4)?.Trim() ?? "",
                    Order3 = parts.ElementAtOrDefault(5)?.Trim() ?? "",
                    Order4 = parts.ElementAtOrDefault(6)?.Trim() ?? ""
                };
            }
        }

        private (string row, string letter) GetRowAndSeatLetter(string seat)
        {
            if (string.IsNullOrWhiteSpace(seat)) return ("", "");
            seat = seat.Trim().ToUpper();
            string letter = seat[^1].ToString();

            if (!int.TryParse(seat[..^1], out int rowNum))
                return ("", letter);

            if (rowNum <= 5) return ("front", letter);
            if (rowNum <= 10) return ("near", letter);
            return ("back", letter);
        }

        // -----------------------------
        // Z Index / Route / Movement / Orders
        // -----------------------------
        private void SetStewardessZIndex(string row)
        {
            currentRowForZ = row;

            int z = row switch
            {
                "back" => 3,
                "near" => 6,
                "front" => 9,
                _ => 9
            };

            Panel.SetZIndex(Stewardess, z);
        }

        private async Task StartRouteSafeAsync()
        {
            try { await RunServingRouteAsync(); }
            catch (Exception ex) { Debug.WriteLine("[ROUTE] EXCEPTION: " + ex); }
        }

        private double PassengerXOffset(string seatLetter)
        {
            seatLetter = (seatLetter ?? "").Trim().ToUpper();
            return seatLetter switch
            {
                "D" => -60,
                "E" => -40,
                "F" => -30,
                _ => 0
            };
        }

        private async Task RunServingRouteAsync()
        {
            if (Stewardess == null) return;

            var rowsWithPassengers = passengers
                .Select(p => GetRowAndSeatLetter(p.Sitzplatz).row)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            var routeRows = new List<string>();
            foreach (var r in new[] { "back", "near", "front" })
                if (rowsWithPassengers.Contains(r))
                    routeRows.Add(r);

            if (routeRows.Count == 0)
                return;

            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);
            EnsureScaleTransform();
            SetStewardessZIndex("front");
            SetStewardessImage("stfront.png");

            string topRow = routeRows[0];
            Canvas.SetLeft(Stewardess, MiddleX);

            await MoveStewardessVerticalWithScale(
                StartY,
                GangY(topRow),
                RowScale("front"),
                RowScale(topRow),
                topRow
            );

            for (int i = 0; i < routeRows.Count; i++)
            {
                string row = routeRows[i];

                await MoveStewardessHorizontal(MiddleX, row);

                await MoveStewardessVerticalWithScale(
                    Canvas.GetTop(Stewardess),
                    GangY(row),
                    ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                    RowScale(row),
                    row
                );

                var paxInRow = passengers
                    .Where(p => GetRowAndSeatLetter(p.Sitzplatz).row == row)
                    .OrderBy(p => AvatarCenterX(p))
                    .ToList();

                foreach (var p in paxInRow)
                {
                    double paxCenter = AvatarCenterX(p);
                    if (paxCenter <= 0) continue;

                    double targetLeft = StewardessLeftFromPassengerCenter(paxCenter);
                    var seatLetter = GetRowAndSeatLetter(p.Sitzplatz).letter;
                    targetLeft += PassengerXOffset(seatLetter);

                    await ServePassengerSimpleAsync(targetLeft, row, p);
                }

                await MoveStewardessHorizontal(MiddleX, row);

                if (i < routeRows.Count - 1)
                {
                    string nextRow = routeRows[i + 1];
                    Canvas.SetLeft(Stewardess, MiddleX);

                    await MoveStewardessVerticalWithScale(
                        Canvas.GetTop(Stewardess),
                        GangY(nextRow),
                        ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                        RowScale(nextRow),
                        nextRow
                    );
                }
            }

            await MoveStewardessHorizontal(MiddleX, "front");
            await MoveStewardessVerticalWithScale(
                Canvas.GetTop(Stewardess),
                StartY,
                ((ScaleTransform)Stewardess.RenderTransform).ScaleX,
                RowScale("front"),
                "front"
            );

            Canvas.SetLeft(Stewardess, MiddleX);
            Canvas.SetTop(Stewardess, StartY);
            var t = (ScaleTransform)Stewardess.RenderTransform;
            t.ScaleX = RowScale("front");
            t.ScaleY = RowScale("front");
            SetStewardessZIndex("front");
            SetStewardessImage("stfront.png");
        }

        private double AvatarCenterX(Passenger p)
        {
            var avatar = FindAvatarOf(p);
            if (avatar == null) return 0;
            return Canvas.GetLeft(avatar) + (avatar.Width / 2.0);
        }

        private FrameworkElement FindAvatarOf(Passenger p)
        {
            string tag = $"avatar_passenger_{(p.Name ?? "").Trim()}";
            return CabinCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(e => (e.Tag as string) == tag);
        }

        private double StewardessLeftFromPassengerCenter(double passengerCenterX)
            => passengerCenterX - (Stewardess.Width / 2.0);

        // -----------------------------
        // Orders: Bubble erscheint erst beim Servieren + Icons kommen nach und nach
        // -----------------------------
        private List<string> GetOrdersOfPassenger(Passenger p)
        {
            return new[]
            {
                p.Order1, p.Order2, p.Order3, p.Order4
            }
            .Select(o => (o ?? "").Trim())
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToList();
        }

        private int GetServedCount(Passenger p)
        {
            if (p == null) return 0;
            if (!servedOrders.ContainsKey(p)) servedOrders[p] = 0;
            return servedOrders[p];
        }

        private string GetNextOrderFileAndAdvance(Passenger p)
        {
            if (p == null) return null;

            if (!servedOrders.ContainsKey(p))
                servedOrders[p] = 0;

            var orders = GetOrdersOfPassenger(p);
            int index = servedOrders[p];

            if (index >= orders.Count)
                return null;

            string file = orders[index];
            servedOrders[p] = index + 1;
            return file;
        }

        private Border CreateOrderBubbleHidden(Passenger p, string row, out Image[] iconRefs)
        {
            // ✅ Bubble-Größe wird von außen gesetzt (bubble.Width/Height in LoadAndPlacePassengers),
            // aber falls nicht: Default.
            double bubbleSize = 90;

            // ✅ enger & sicher (damit nichts abgeschnitten wird)
            const double padding = 5;        // Innenabstand der Bubble
            const double iconMargin = 1;     // Abstand je Icon im Slot

            // 2x2 Raster: verfügbare Innenfläche
            double inner = bubbleSize - (padding * 2);

            // Icon-Größe pro Zelle so, dass Margin NICHT clippt:
            // pro Zelle: inner/2, davon gehen links+rechts margin weg -> 2*iconMargin
            double iconSize = (inner / 2.0) - (iconMargin * 2);

            // Sicherheitsnetz (falls jemand bubbleSize kleiner macht)
            if (iconSize < 10) iconSize = 10;

            var grid = new Grid
            {
                Width = bubbleSize,
                Height = bubbleSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            iconRefs = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                var img = new Image
                {
                    Width = iconSize,
                    Height = iconSize,
                    Stretch = Stretch.Uniform,               // ✅ nichts abschneiden
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(iconMargin),
                    Opacity = 0.0,                           // ✅ erscheint erst beim Servieren
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };

                iconRefs[i] = img;

                Grid.SetRow(img, i / 2);
                Grid.SetColumn(img, i % 2);
                grid.Children.Add(img);
            }

            var bubble = new Border
            {
                Child = grid,
                Width = bubbleSize,
                Height = bubbleSize,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 165, 0)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(padding),

                // ✅ Position NICHT hier hacken – wird vom Container geregelt
                Margin = new Thickness(0),

                IsHitTestVisible = false,
                Opacity = 0.0,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Color = Colors.Black,
                    Opacity = 0.50
                },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            return bubble;
        }

        private void EnsureBubbleVisible(Passenger p)
        {
            if (!orderBubbleRefs.TryGetValue(p, out var bubble) || bubble == null) return;

            if (bubble.Visibility != Visibility.Visible)
            {
                bubble.Visibility = Visibility.Visible;

                // Fade-In
                bubble.Opacity = 0;
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
                bubble.BeginAnimation(OpacityProperty, fade);
            }
        }

        private void RevealNextOrderIcon(Passenger p, string orderFileName, int servedIndexJustServed)
        {
            if (!orderIconRefs.TryGetValue(p, out var icons) || icons == null) return;
            if (servedIndexJustServed < 0 || servedIndexJustServed > 3) return;

            // 🔧 Pfad aus config.json
            string ordersDir = ConfigService.Current.Paths.Orders; // ggf. anpassen
            string full = Path.Combine(ordersDir, orderFileName);

            if (!File.Exists(full))
            {
                Debug.WriteLine($"[ORDER] Missing: {full}");
                return;
            }

            var img = icons[servedIndexJustServed];
            img.Source = LoadBitmapNoLock(full);

            // sichtbar machen (mit kleinem Pop)
            img.Opacity = 0.0;
            var fade = new DoubleAnimation(0, 0.95, TimeSpan.FromMilliseconds(120));
            img.BeginAnimation(OpacityProperty, fade);
        }

        // -----------------------------
        // Serve: Bubble + Order erscheinen erst beim Servieren
        // -----------------------------
        private async Task ServePassengerSimpleAsync(double targetLeftX, string rowForZ, Passenger p)
        {
            await MoveStewardessHorizontal(targetLeftX, rowForZ);

            bool serviceToLeftSide = targetLeftX < MiddleX;
            SetStewardessImage(serviceToLeftSide ? "stservicelinks.png" : "stservicerechts.png");

            for (int i = 0; i < 2; i++)
            {
                Canvas.SetLeft(Stewardess, targetLeftX + 6);
                await Task.Delay(160);
                Canvas.SetLeft(Stewardess, targetLeftX - 6);
                await Task.Delay(160);
            }
            Canvas.SetLeft(Stewardess, targetLeftX);

            // ✅ Bubble erscheint erst beim Servieren
            EnsureBubbleVisible(p);

            // ✅ Sobald sie da ist: ALLE Orders anzeigen (nicht nur eine)
            PopulateBubbleWithAllOrders(p);

            // ✅ OPTIONAL: Wenn du die "served"-Logik intern behalten willst (ohne Dimmung),
            // dann nur zählen, aber NICHT visualisieren:
            _ = GetNextOrderFileAndAdvance(p);

            await MoveStewardessHorizontal(MiddleX, rowForZ);
        }

        // -----------------------------
        // Movement helpers
        // -----------------------------
        private void EnsureScaleTransform()
        {
            if (Stewardess.RenderTransform is not ScaleTransform)
                Stewardess.RenderTransform = new ScaleTransform(RowScale("front"), RowScale("front"));
        }

        private async Task MoveStewardessHorizontal(double targetX, string rowForZ)
        {
            BeginMovementSound();
            try
            {
                SetStewardessZIndex(rowForZ);

                double startX = Canvas.GetLeft(Stewardess);
                if (targetX < startX) SetStewardessImage("stlinks.png");
                else if (targetX > startX) SetStewardessImage("strechts.png");

                double speedFactor = 0.08;
                double minStep = 0.35;

                double x = Canvas.GetLeft(Stewardess);

                while (Math.Abs(x - targetX) > 0.5)
                {
                    SetStewardessZIndex(rowForZ);

                    double delta = (targetX - x) * speedFactor;
                    if (Math.Abs(delta) < minStep) delta = Math.Sign(delta) * minStep;

                    x += delta;
                    Canvas.SetLeft(Stewardess, x);

                    await Task.Delay(16);
                }

                Canvas.SetLeft(Stewardess, targetX);
                SetStewardessZIndex(rowForZ);
            }
            finally
            {
                EndMovementSound();
            }
        }

        private void PopulateBubbleWithAllOrders(Passenger p)
        {
            if (p == null) return;
            if (!orderIconRefs.TryGetValue(p, out var icons) || icons == null) return;

            var orders = GetOrdersOfPassenger(p);
            if (orders.Count == 0) return;

            string ordersDir = ConfigService.Current.Paths.Orders; // ggf. anpassen

            for (int i = 0; i < 4; i++)
            {
                var img = icons[i];
                if (img == null) continue;

                if (i < orders.Count)
                {
                    string fileName = orders[i];
                    string full = Path.Combine(ordersDir, fileName);

                    if (File.Exists(full))
                    {
                        img.Source = LoadBitmapNoLock(full);
                        img.Opacity = 0.95;   // sichtbar
                        img.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Debug.WriteLine($"[ORDER] Missing: {full}");
                        img.Opacity = 0.0;
                        img.Visibility = Visibility.Hidden;
                    }
                }
                else
                {
                    // kein Order in dem Slot
                    img.Source = null;
                    img.Opacity = 0.0;
                    img.Visibility = Visibility.Hidden;
                }
            }

            // direkt korrekt "Served" darstellen (falls schon was serviert wurde)
            RefreshServedDimming(p);
        }

        private void RefreshServedDimming(Passenger p)
        {
            if (p == null) return;
            if (!orderIconRefs.TryGetValue(p, out var icons) || icons == null) return;

            int served = GetServedCount(p);
            var orders = GetOrdersOfPassenger(p);

            for (int i = 0; i < 4; i++)
            {
                var img = icons[i];
                if (img == null) continue;

                if (i >= orders.Count)
                    continue;

                // ✅ served -> nur leicht dimmen (nicht "weg")
                if (i < served)
                {
                    img.Opacity = 0.55;     // vorher war das viel zu niedrig (wirkt wie "hinter Bubble")
                    img.Effect = new DropShadowEffect
                    {
                        BlurRadius = 2,
                        ShadowDepth = 0,
                        Color = Colors.Black,
                        Opacity = 0.35
                    };
                }
                else
                {
                    img.Opacity = 0.95;
                    img.Effect = null;
                }
            }
        }

        private async Task MoveStewardessVerticalWithScale(double startY, double targetY, double startScale, double targetScale, string rowForZ)
        {
            BeginMovementSound();
            try
            {
                EnsureScaleTransform();
                var transform = (ScaleTransform)Stewardess.RenderTransform;

                SetStewardessZIndex(rowForZ);

                bool goingUp = targetY < startY;
                SetStewardessImage(goingUp ? "stheck.png" : "stfront.png");

                double speed = 3;
                int direction = targetY > startY ? 1 : -1;

                while (Math.Abs(Canvas.GetTop(Stewardess) - targetY) > 0.5)
                {
                    SetStewardessZIndex(rowForZ);

                    double currentY = Canvas.GetTop(Stewardess);
                    double newY = currentY + direction * speed;

                    if ((direction > 0 && newY > targetY) || (direction < 0 && newY < targetY))
                        newY = targetY;

                    Canvas.SetTop(Stewardess, newY);

                    double denom = Math.Abs(targetY - startY);
                    double progress = denom <= 0.0001 ? 1.0 : Math.Abs(newY - startY) / denom;

                    double scale = startScale + (targetScale - startScale) * progress;
                    transform.ScaleX = scale;
                    transform.ScaleY = scale;

                    await Task.Delay(16);
                }

                Canvas.SetTop(Stewardess, targetY);
                transform.ScaleX = targetScale;
                transform.ScaleY = targetScale;

                SetStewardessZIndex(rowForZ);
            }
            finally
            {
                EndMovementSound();
            }
        }
    }
}
