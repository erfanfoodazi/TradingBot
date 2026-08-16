using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TradingBot.UI.Charts;
using TradingBot.UI.ViewModels;

namespace TradingBot.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(
            MainViewModel vm,
            ChartService chartService)
        {
            InitializeComponent();

            DataContext = vm;

            chartService.Attach(Chart);
            chartService.AttachOscillator(OscillatorChart);

            Loaded += async (_, _) => await vm.InitializeCommand.ExecuteAsync(null);
        }
    }
}
