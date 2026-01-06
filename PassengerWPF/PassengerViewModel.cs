using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PassengerWPF
{
    public class PassengerViewModel : INotifyPropertyChanged
    {
        private string name;
        public string Name { get => name; set => SetField(ref name, value); }

        private string sitzplatz;
        public string Sitzplatz { get => sitzplatz; set => SetField(ref sitzplatz, value); }

        // Avatar (Dateiname) und Pfad
        private string avatar;
        public string Avatar
        {
            get => avatar;
            set
            {
                if (SetField(ref avatar, value))
                {
                    // AvatarImagePath wird so gesetzt, dass es auf eine existierende Datei zeigt:
                    // 1) wenn value ist ein absoluter Pfad und existiert -> nehmen
                    // 2) sonst versuchen in ConfigService.Current.Paths.Avatars
                    // 3) sonst placeholder
                    AvatarImagePath = ResolveFullPath(value, ConfigService.Current.Paths.Avatars);
                }
            }
        }

        private string avatarImagePath;
        public string AvatarImagePath
        {
            get => avatarImagePath;
            set => SetField(ref avatarImagePath, value);
        }

        // Orders: 4 einzelne Properties (bindbar)
        private string order1;
        public string Order1
        {
            get => order1;
            set
            {
                if (SetField(ref order1, value))
                    Order1ImagePath = ResolveFullPath(value, ConfigService.Current.Paths.Orders);
            }
        }
        private string order1ImagePath;
        public string Order1ImagePath { get => order1ImagePath; set => SetField(ref order1ImagePath, value); }

        private string order2;
        public string Order2
        {
            get => order2;
            set
            {
                if (SetField(ref order2, value))
                    Order2ImagePath = ResolveFullPath(value, ConfigService.Current.Paths.Orders);
            }
        }
        private string order2ImagePath;
        public string Order2ImagePath { get => order2ImagePath; set => SetField(ref order2ImagePath, value); }

        private string order3;
        public string Order3
        {
            get => order3;
            set
            {
                if (SetField(ref order3, value))
                    Order3ImagePath = ResolveFullPath(value, ConfigService.Current.Paths.Orders);
            }
        }
        private string order3ImagePath;
        public string Order3ImagePath { get => order3ImagePath; set => SetField(ref order3ImagePath, value); }

        private string order4;
        public string Order4
        {
            get => order4;
            set
            {
                if (SetField(ref order4, value))
                    Order4ImagePath = ResolveFullPath(value, ConfigService.Current.Paths.Orders);
            }
        }
        private string order4ImagePath;
        public string Order4ImagePath { get => order4ImagePath; set => SetField(ref order4ImagePath, value); }

        public event PropertyChangedEventHandler PropertyChanged;

        // Hilfsfunktion: bestimmt einen existierenden vollen Pfad oder placeholder
        private string ResolveFullPath(string value, string folder)
        {
            // placeholder-Dateiname (relativ zum folder)
            string placeholder = Path.Combine(folder ?? "", "placeholder.png");

            if (string.IsNullOrEmpty(value))
            {
                return File.Exists(placeholder) ? placeholder : "";
            }

            // ist value ein absoluter Pfad, der existiert?
            if (Path.IsPathRooted(value) && File.Exists(value))
                return value;

            // sonst versuche folder + value
            if (!string.IsNullOrEmpty(folder))
            {
                var combined = Path.Combine(folder, value);
                if (File.Exists(combined)) return combined;
            }

            // Fallback: wenn value selbst (evtl. relative) existiert
            if (File.Exists(value)) return Path.GetFullPath(value);

            // letzter Fallback placeholder (wenn vorhanden)
            if (File.Exists(placeholder)) return placeholder;

            return ""; // nichts vorhanden
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
