using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PassengerWPF
{
    public partial class FrequentFlyerWindow : Window
    {
        private readonly string boardingCountFile;
        private readonly string passengerDataFile;

        // Set für Passagiere, die schon beim aktuellen Flug gezählt wurden
        private readonly HashSet<string> alreadyProcessed = new();

        public FrequentFlyerWindow()
        {
            InitializeComponent();

            // Hintergrundbild laden
            string bgPath = ConfigService.Current.Paths.FrequentFlyerBg;
            if (File.Exists(bgPath))
            {
                try
                {
                    var uri = new Uri(bgPath, UriKind.Absolute);
                    BgImageControl.Source = new BitmapImage(uri);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden des Hintergrundbilds:\n{ex.Message}",
                                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // CSV-Dateien
            boardingCountFile = ConfigService.Current.Csv.BoardingCount;
            passengerDataFile = ConfigService.Current.Csv.PassengerData;
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            ItemsPanel.Children.Clear();

            if (!File.Exists(boardingCountFile))
            {
                File.WriteAllText(boardingCountFile, "Name,Count,LastFlightId\r\n");
            }

            if (!File.Exists(passengerDataFile))
            {
                MessageBox.Show($"PassengerData-Datei nicht gefunden:\n{passengerDataFile}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Aktuelle FlightID
                string currentFlightId = ConfigService.Current.Csv.CurrentFlightId;

                // BoardingCount.csv einlesen
                var boardingLines = File.ReadAllLines(boardingCountFile)
                                        .Skip(1)
                                        .Where(l => !string.IsNullOrWhiteSpace(l))
                                        .Select(l =>
                                        {
                                            var parts = l.Split(',');
                                            string name = parts[0].Trim();
                                            int count = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 0;
                                            string lastFlightId = parts.Length > 2 ? parts[2] : "";
                                            return new { Name = name, Count = count, LastFlightId = lastFlightId };
                                        })
                                        .ToDictionary(p => p.Name, p => new { p.Count, p.LastFlightId });

                // PassengerData.csv einlesen
                var passengerLines = File.ReadAllLines(passengerDataFile)
                                         .Skip(1)
                                         .Where(l => !string.IsNullOrWhiteSpace(l))
                                         .Select(l => l.Split(',')[0].Trim())
                                         .Distinct();

                // Neue Passagiere erkennen & Count aktualisieren
                foreach (var passenger in passengerLines)
                {
                    if (boardingLines.ContainsKey(passenger))
                    {
                        if (boardingLines[passenger].LastFlightId != currentFlightId)
                        {
                            // Count erhöhen
                            boardingLines[passenger] = new
                            {
                                Count = boardingLines[passenger].Count + 1,
                                LastFlightId = currentFlightId
                            };
                        }
                    }
                    else
                    {
                        // Neu aufnehmen
                        boardingLines[passenger] = new { Count = 1, LastFlightId = currentFlightId };
                    }
                }

                // BoardingCount.csv speichern
                using (var writer = new StreamWriter(boardingCountFile))
                {
                    writer.WriteLine("Name,Count,LastFlightId");
                    foreach (var entry in boardingLines.OrderByDescending(k => k.Value.Count).ThenBy(k => k.Key))
                    {
                        writer.WriteLine($"{entry.Key},{entry.Value.Count},{entry.Value.LastFlightId}");
                    }
                }


                // GUI aktualisieren (wie bisher)
                int rowIndex = 0;
                var header = new TextBlock
                {
                    Text = string.Format("{0,-30} {1,10}", "Name", "Flüge"),
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = Brushes.LightGoldenrodYellow,
                    Background = new SolidColorBrush(Color.FromArgb(160, 40, 30, 20)),
                    Padding = new Thickness(5)
                };
                ItemsPanel.Children.Add(header);

                var line = new TextBlock
                {
                    Text = new string('-', 42),
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = Brushes.LightGoldenrodYellow,
                    Background = new SolidColorBrush(Color.FromArgb(160, 40, 30, 20)),
                    Padding = new Thickness(5)
                };
                ItemsPanel.Children.Add(line);

                foreach (var entry in boardingLines.OrderByDescending(k => k.Value.Count).ThenBy(k => k.Key))
                {
                    var tb = new TextBlock
                    {
                        Text = string.Format("{0,-30} {1,10}", entry.Key, entry.Value.Count),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Foreground = Brushes.LightGoldenrodYellow,
                        Background = rowIndex % 2 == 0
                            ? new SolidColorBrush(Color.FromArgb(160, 40, 30, 20))
                            : new SolidColorBrush(Color.FromArgb(160, 60, 45, 35)),
                        Padding = new Thickness(5)
                    };
                    ItemsPanel.Children.Add(tb);
                    rowIndex++;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden oder Speichern der Daten:\n{ex.Message}",
                                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
