using System.Windows;

namespace PassengerWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigService.Load();

            var main = new MainWindow();
            Current.MainWindow = main;
            main.Show();
        }
    }
}
