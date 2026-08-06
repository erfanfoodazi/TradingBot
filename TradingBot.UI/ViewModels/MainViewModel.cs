using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMarketDataService _marketDataService;
    private readonly ITradingService _tradingService;

    [ObservableProperty]
    private List<SymbolResponseDto> symbols = [];

    [ObservableProperty]
    private SymbolResponseDto? selectedSymbol;

    [ObservableProperty]
    private string selectedTimeframe = "M1";

    [ObservableProperty]
    private int candleCount = 500;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private string status = "";

    [ObservableProperty]
    private bool isBusy;


    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var health = await _marketDataService.HealthAsync();

            IsConnected = health.Connected;
            Status = health.Status;

            Symbols = await _marketDataService.GetSymbolsAsync();

            if (Symbols.Count > 0)
                SelectedSymbol = Symbols[0];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadCandlesAsync()
    {
        if (SelectedSymbol == null)
            return;

        var candles = await _marketDataService.GetHistoryAsync(
            new CandleHistoryRequestDto
            {
                Symbol = SelectedSymbol.Name,
                Timeframe = SelectedTimeframe,
                Count = CandleCount
            });

    }
}