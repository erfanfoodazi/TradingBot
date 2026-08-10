using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Windows;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Database;
using Trading.Infrastructure.Options;
using Trading.Infrastructure.Services;
using TradingBot.UI.Charts;
using TradingBot.UI.Themes;
using TradingBot.UI.ViewModels;

namespace TradingBot.UI;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;

    public App()
    {
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var pythonSection = context.Configuration.GetSection(PythonApiOptions.SectionName);
                services.Configure<PythonApiOptions>(pythonSection);

                var connectionString = context.Configuration.GetConnectionString("Default");
                services.AddDbContextFactory<TradingDbContext>(options =>
                    options.UseSqlServer(connectionString));

                services.AddHttpClient<IPythonApiClient, PythonApiClient>();

                services.AddSingleton<IRealtimeService, RealtimeService>();
                services.AddSingleton<IRealtimeSession, RealtimeSession>();
                services.AddHostedService<RealtimeConnectionWorker>();
                services.AddSingleton<IMarketDataService, MarketDataService>();
                services.AddSingleton<ITradingService, TradingService>();
                services.AddSingleton<IAccountService, AccountService>();
                services.AddSingleton<IAppLogger, AppSqlLogger>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IWatchlistService, WatchlistService>();
                services.AddSingleton<IStrategyService, StrategyService>();

                services.AddSingleton<ChartService>();
                services.AddSingleton<IChartService>(sp => sp.GetRequiredService<ChartService>());
                services.AddSingleton<ThemeService>();

                services.AddSingleton<PythonApiHost>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await Host.StartAsync();

        var pythonHost = Host.Services.GetRequiredService<PythonApiHost>();
        pythonHost.Start();
        await pythonHost.WaitUntilReadyAsync(TimeSpan.FromSeconds(15));

        await EnsureDatabaseAsync();

        var window = Host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            var pythonHost = (PythonApiHost?)Host.Services.GetService(typeof(PythonApiHost));
            pythonHost?.Stop();
        }
        catch { /* best effort */ }

        try
        {
            var realtime = (IRealtimeService?)Host.Services.GetService(typeof(IRealtimeService));
            if (realtime is not null)
                await realtime.DisconnectAsync();
        }
        catch { /* best effort */ }

        await Host.StopAsync();
        Host.Dispose();

        base.OnExit(e);
    }

    private static async Task EnsureDatabaseAsync()
    {
        try
        {
            var factory = Host.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<TradingDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            System.Diagnostics.Debug.WriteLine("Trading database migrated successfully.");
        }
        catch (Exception ex)
        {
            // The SQL Server may be offline; the app still runs with degraded persistence.
            System.Diagnostics.Debug.WriteLine($"Trading database unavailable: {ex.Message}");
        }
    }
}