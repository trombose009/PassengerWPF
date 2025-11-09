using PassengerWPF;
using System.Windows; // WPF

namespace PassengerWPF
{
    public partial class App : System.Windows.Application // <-- explizit WPF
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigService.Load();

            var main = new MainMenuWindow();
            Application.Current.MainWindow = main;
            main.Show();
        }


    }
}
