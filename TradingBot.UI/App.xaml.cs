using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Services;

namespace TradingBot.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    public partial class App : Application
    {
        public static IHost Host { get; private set; } = null!;

        public App()
        {
            Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // HttpClient
                    services.AddHttpClient<IPythonApiClient, PythonApiClient>(client =>
                    {
                        client.BaseAddress = new Uri("http://127.0.0.1:8000");
                        client.Timeout = TimeSpan.FromSeconds(30);
                    });

                    // Services
                    services.AddSingleton<IMarketDataService, MarketDataService>();
                    services.AddSingleton<ITradingService, TradingService>();

                    // Windows
                    services.AddSingleton<MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await Host.StartAsync();

            var window = Host.Services.GetRequiredService<MainWindow>();
            window.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await Host.StopAsync();
            Host.Dispose();

            base.OnExit(e);
        }
    }

}
