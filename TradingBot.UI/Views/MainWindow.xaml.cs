using System.ComponentModel;
using System.Windows;
using Trading.Infrastructure.Services;
using TradingBot.UI.Charts;
using TradingBot.UI.ViewModels;

namespace TradingBot.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PythonApiHost _pythonApiHost;

        public MainWindow(
            MainViewModel vm,
            ChartService chartService,
            PythonApiHost pythonApiHost)
        {
            _pythonApiHost = pythonApiHost;
            InitializeComponent();

            DataContext = vm;

            chartService.Attach(Chart);
            chartService.AttachOscillator(OscillatorChart);

            // Stop the Python backend as soon as the window closes so it cannot
            // linger after the UI is gone. App.OnExit calls Stop() again as a
            // safety net - the host is idempotent, so a double stop is harmless.
            Closing += OnClosing;

            Loaded += async (_, _) => await vm.InitializeCommand.ExecuteAsync(null);
        }

        private void OnClosing(object? sender, CancelEventArgs e)
            => _pythonApiHost.Stop();
    }
}