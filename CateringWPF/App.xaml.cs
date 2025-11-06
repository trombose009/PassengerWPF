using CateringWPF;
using System.Windows; // WPF

namespace CateringWPF
{
    public partial class App : System.Windows.Application // <-- explizit WPF
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Config laden
            ConfigService.Load();

            // MainMenuWindow starten
            var mainMenu = new MainMenuWindow();
            mainMenu.Show();
        }
    }
}
