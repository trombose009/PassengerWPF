using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace PassengerWPF
{
    public partial class LiveBoardingWindow : Window
    {
        private string basePath;
        private string csvPath;
        private string countCsvPath;
        private string actualFlightPath;
        private string soundPath;
        private string outputImagePath;
        private string bgImagePath;

        private Dictionary<string, WinForms.Button> seatButtons = new();
        private List<WinForms.Label> boardingListLabels = new();

        private WinForms.Panel scrollPanel;
        private WinForms.Panel listPanel;
        private WinForms.PictureBox pictureBox;
        private Bitmap bgImage;
        private WinForms.Timer timer;
        private WinForms.Panel statusLight;
        private WinForms.Button toggleButton;

        public LiveBoardingWindow()
        {
            InitializeComponent();

            // Basisverzeichnis
            basePath = System.IO.Path.Combine(
                Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName,
                "boarding");

            csvPath = Path.Combine(basePath, "boarding.csv");
            countCsvPath = Path.Combine(basePath, "boarding_count.csv");
            actualFlightPath = Path.Combine(basePath, "actualflight.csv");
            soundPath = Path.Combine(basePath, "bingbing.wav");
            outputImagePath = Path.Combine(basePath, "boarding_render.png");
            bgImagePath = Path.Combine(basePath, "bg.png");

            SetupWinFormsHost();
            LoadSeats();
            SetupListPanel();
            SetupControls();
            UpdateBoardingListFromActualFlight();
        }

        private void SetupWinFormsHost()
        {
            // ScrollPanel (für Sitzplan)
            scrollPanel = new WinForms.Panel
            {
                Location = new System.Drawing.Point(130, 60),
                Size = new System.Drawing.Size(400, 900),
                AutoScroll = true,
                BorderStyle = WinForms.BorderStyle.FixedSingle
            };

            // Hintergrundbild
            if (!File.Exists(bgImagePath))
            {
                System.Windows.MessageBox.Show($"Hintergrundbild nicht gefunden: {bgImagePath}");
                Close();
            }
            else
            {
                bgImage = (Bitmap)Bitmap.FromFile(bgImagePath);
                pictureBox = new WinForms.PictureBox
                {
                    Image = bgImage,
                    SizeMode = WinForms.PictureBoxSizeMode.StretchImage,
                    Size = new System.Drawing.Size(400, 1500),
                    Location = new System.Drawing.Point(0, 0)
                };
                scrollPanel.Controls.Add(pictureBox);
            }

            // Host für WPF
            var host = new WindowsFormsHost
            {
                Child = scrollPanel
            };
            Grid.SetRow(host, 0);
            MainGrid.Children.Add(host);
        }

        private void LoadSeats()
        {
            string[] seatsAll = { "A", "B", "C", "D", "E", "F" };
            int totalRows = 17;
            int seatWidth = 24, seatHeight = 27, xOffset = 128, yOffset = 305, colSpacing = 20, aisleSpacing = 30, rowSpacing = 36;
            var smallFont = new System.Drawing.Font("Segoe UI", 6.5f);
            var toolTip = new WinForms.ToolTip();

            for (int row = 1; row <= totalRows; row++)
            {
                for (int i = 0; i < seatsAll.Length; i++)
                {
                    string seatLabel = $"{row}{seatsAll[i]}";
                    var btn = new WinForms.Button
                    {
                        Text = seatLabel,
                        Font = smallFont,
                        Width = seatWidth,
                        Height = seatHeight,
                        UseVisualStyleBackColor = true,
                        BackColor = row <= 5 ? System.Drawing.Color.LightBlue : System.Drawing.Color.LightGray
                    };

                    int x = xOffset + (i * colSpacing);
                    if (i >= 3) x += aisleSpacing;
                    int y = yOffset + ((row - 1) * rowSpacing);
                    btn.Location = new System.Drawing.Point(x, y);

                    seatButtons[seatLabel.ToUpper()] = btn;
                    scrollPanel.Controls.Add(btn);
                    btn.BringToFront();
                }
            }
        }

        private void SetupListPanel()
        {
            listPanel = new WinForms.Panel
            {
                Location = new System.Drawing.Point(5, 60),
                Size = new System.Drawing.Size(120, 800),
                AutoScroll = true,
                BorderStyle = WinForms.BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.FromArgb(179, 255, 255)
            };
            var host = new WindowsFormsHost { Child = listPanel };
            Grid.SetRow(host, 0);
            MainGrid.Children.Add(host);
        }

        private void SetupControls()
        {
            toggleButton = new WinForms.Button
            {
                Text = "Start",
                Size = new System.Drawing.Size(80, 30),
                Location = new System.Drawing.Point(10, 10)
            };
            toggleButton.Click += ToggleButton_Click;
            scrollPanel.Controls.Add(toggleButton);

            statusLight = new WinForms.Panel
            {
                Size = new System.Drawing.Size(20, 20),
                Location = new System.Drawing.Point(385, 15),
                BackColor = System.Drawing.Color.Gray
            };
            System.Drawing.Drawing2D.GraphicsPath gp = new();
            gp.AddEllipse(0, 0, 20, 20);
            statusLight.Region = new System.Drawing.Region(gp);
            scrollPanel.Controls.Add(statusLight);

            timer = new WinForms.Timer { Interval = 5000 };
            timer.Tick += (s, e) => ProcessBoarding();
        }

        private void ToggleButton_Click(object sender, EventArgs e)
        {
            if (timer.Enabled)
            {
                timer.Stop();
                toggleButton.Text = "Start";
                statusLight.BackColor = System.Drawing.Color.Gray;
            }
            else
            {
                timer.Start();
                toggleButton.Text = "Stop";
                statusLight.BackColor = System.Drawing.Color.LimeGreen;
            }
        }

        private void UpdateBoardingListFromActualFlight()
        {
            foreach (var lbl in boardingListLabels)
            {
                listPanel.Controls.Remove(lbl);
                lbl.Dispose();
            }
            boardingListLabels.Clear();

            if (!File.Exists(actualFlightPath)) return;
            var actualPassengers = File.Exists(actualFlightPath) ? ImportCsv(actualFlightPath) : new List<(string Name, string Seat)>();

            int yPos = 5;
            var font = new System.Drawing.Font("Segoe UI", 8);
            foreach (var p in actualPassengers)
            {
                if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Seat)) continue;

                if (seatButtons.ContainsKey(p.Seat))
                {
                    var btn = seatButtons[p.Seat];
                    btn.BackColor = System.Drawing.Color.LawnGreen;
                    var tip = new WinForms.ToolTip();
                    tip.SetToolTip(btn, p.Name);
                    btn.BringToFront();
                }

                var label = new WinForms.Label
                {
                    Text = $"{p.Name} --> {p.Seat}",
                    Font = font,
                    AutoSize = true,
                    Location = new System.Drawing.Point(5, yPos)
                };
                listPanel.Controls.Add(label);
                boardingListLabels.Add(label);
                yPos += 20;
            }
        }

        private void ProcessBoarding()
        {
            // CSV-Import und Sitzzuweisung hier umsetzen (analog PS-Skript)
            // Sound abspielen etc.
        }

        private List<(string Name, string Seat)> ImportCsv(string path)
        {
            var list = new List<(string, string)>();
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                    list.Add((parts[0], parts[1].ToUpper()));
            }
            return list;
        }

        private void PlaySound(string path)
        {
            if (!File.Exists(path)) return;
            using var player = new SoundPlayer(path);
            player.Load();
            player.Play();
        }
    }
}
