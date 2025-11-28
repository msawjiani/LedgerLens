using LedgerLens.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace LedgerLens.App
{
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // ViewModels
                    services.AddTransient<MainWindowViewModel>();   // <- adjust namespace if needed

                    // Views
                    services.AddTransient<MainWindow>();
                })
                .Build();

            var main = AppHost.Services.GetRequiredService<MainWindow>();
            MainWindow = main;
            main.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (AppHost is not null) await AppHost.StopAsync();
            AppHost?.Dispose();
            base.OnExit(e);
        }
    }
}
