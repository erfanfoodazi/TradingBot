using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Trading.Core.Indicators;
using Trading.Core.Interfaces;
using Trading.Shared.Enums;
using Trading.Shared.Events;
using Trading.Shared.Requests;
using Trading.Shared.Responses;
using TradingBot.UI.Models;
using TradingBot.UI.Strategy;
using TradingBot.UI.Themes;

namespace TradingBot.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMarketDataService _marketDataService;
    private readonly ITradingService _tradingService;
    private readonly IChartService _chartService;
    private readonly IRealtimeService _realtimeService;
    private readonly IAccountService _accountService;
    private readonly IWatchlistService _watchlistService;
    private readonly IStrategyService _strategyService;
    private readonly ISettingsService _settingsService;
    private readonly IRealtimeSession _realtimeSession;
    private readonly IAppLogger _logger;
    private readonly ThemeService _themeService;
    private readonly Dispatcher _dispatcher;

    private string _activeSymbol = string.Empty;
    private string _activeTimeframe = "M1";
    private ManualStrategy? _strategy;

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
    private string status = "Starting...";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string symbolFilter = "";

    [ObservableProperty]
    private List<PositionResponseDto> positions = [];

    [ObservableProperty]
    private PositionResponseDto? selectedPosition;

    [ObservableProperty]
    private AccountResponseDto? account;

    [ObservableProperty]
    private double lastBid;

    [ObservableProperty]
    private double lastAsk;

    [ObservableProperty]
    private long candlesReceived;

    // Trade entry
    [ObservableProperty]
    private double volume = 0.1;

    [ObservableProperty]
    private double? stopLoss;

    [ObservableProperty]
    private double? takeProfit;

    // Pending order entry
    [ObservableProperty]
    private PendingOrderType selectedPendingType = PendingOrderType.BuyLimit;

    [ObservableProperty]
    private double pendingOrderPrice;

    [ObservableProperty]
    private double pendingOrderVolume = 0.01;

    [ObservableProperty]
    private double? pendingStopLoss;

    [ObservableProperty]
    private double? pendingTakeProfit;

    [ObservableProperty]
    private DateTime? pendingOrderExpiration;

    [ObservableProperty]
    private List<PendingOrderResponseDto> pendingOrders = [];

    [ObservableProperty]
    private PendingOrderResponseDto? selectedPendingOrder;

    // Modify SL/TP
    [ObservableProperty]
    private double? modifyStopLoss;

    [ObservableProperty]
    private double? modifyTakeProfit;

    // History
    [ObservableProperty]
    private List<TradeHistoryResponseDto> tradeHistory = [];

    // Watchlists
    [ObservableProperty]
    private List<WatchlistResponseDto> watchlists = [];

    [ObservableProperty]
    private WatchlistResponseDto? selectedWatchlist;

    [ObservableProperty]
    private string newWatchlistName = "";

    [ObservableProperty]
    private string newWatchlistSymbols = "";

    // Strategies
    [ObservableProperty]
    private List<StrategyResponseDto> strategies = [];

    [ObservableProperty]
    private StrategyResponseDto? selectedStrategy;

    [ObservableProperty]
    private string newStrategyName = "";

    [ObservableProperty]
    private string newStrategyDescription = "";

    [ObservableProperty]
    private string newStrategyParameters = "{}";

    // Manual strategy
    [ObservableProperty]
    private bool isStrategyEnabled;

    public string StrategyButtonText => IsStrategyEnabled ? "Stop Strategy" : "Start Strategy";

    // Settings
    [ObservableProperty]
    private List<SettingResponseDto> settings = [];

    [ObservableProperty]
    private string newSettingKey = "";

    [ObservableProperty]
    private string newSettingValue = "";

    // Theme
    [ObservableProperty]
    private AppTheme selectedTheme = AppTheme.Light;

    public IEnumerable<AppTheme> Themes => Palette.All;

    public ICollectionView FilteredSymbols { get; private set; }

    public IEnumerable<PendingOrderType> PendingOrderTypes
        => Enum.GetValues<PendingOrderType>();

    /// <summary>Technical indicators selectable from the toolbar dropdown.</summary>
    public IReadOnlyList<IndicatorOption> IndicatorOptions { get; private set; } = [];

    [ObservableProperty]
    private IndicatorOption? selectedIndicator;

    /// <summary>
    /// True while an oscillator-type indicator (RSI/Stochastic/MACD) is applied -
    /// drives the visibility of the strip below the price chart.
    /// </summary>
    [ObservableProperty]
    private bool hasOscillatorIndicator;

    /// <summary>
    /// Applies the indicator currently selected in the toolbar dropdown to the
    /// chart. Indicators update live as new candles arrive.
    /// </summary>
    [RelayCommand]
    private void SetIndicator()
    {
        var type = SelectedIndicator?.Type;
        _chartService.SetIndicators(type is null ? [] : [type.Value]);
        HasOscillatorIndicator =
            type is IndicatorType.Rsi or IndicatorType.Stochastic or IndicatorType.Macd;
    }

    public MainViewModel(
        IMarketDataService marketDataService,
        ITradingService tradingService,
        IChartService chartService,
        IRealtimeService realtimeService,
        IAccountService accountService,
        IWatchlistService watchlistService,
        IStrategyService strategyService,
        ISettingsService settingsService,
        IRealtimeSession realtimeSession,
        IAppLogger logger,
        ThemeService themeService)
    {
        _marketDataService = marketDataService;
        _tradingService = tradingService;
        _chartService = chartService;
        _realtimeService = realtimeService;
        _accountService = accountService;
        _watchlistService = watchlistService;
        _strategyService = strategyService;
        _settingsService = settingsService;
        _realtimeSession = realtimeSession;
        _logger = logger;
        _themeService = themeService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        FilteredSymbols = CollectionViewSource.GetDefaultView(Symbols);
        FilteredSymbols.Filter = SymbolMatchesFilter;

        _themeService.Apply(SelectedTheme);

        WireRealtimeEvents();
        InitializeIndicators();
    }

    private void InitializeIndicators()
    {
        IndicatorOptions =
        [
            new IndicatorOption(null, "None"),
            new IndicatorOption(IndicatorType.Sma, "SMA (20)"),
            new IndicatorOption(IndicatorType.Ema, "EMA (20)"),
            new IndicatorOption(IndicatorType.BollingerBands, "Bollinger Bands (20, 2)"),
            new IndicatorOption(IndicatorType.Vwap, "VWAP"),
            new IndicatorOption(IndicatorType.Atr, "ATR (14)"),
            new IndicatorOption(IndicatorType.Fibonacci, "Fibonacci"),
            new IndicatorOption(IndicatorType.Ichimoku, "Ichimoku"),
            new IndicatorOption(IndicatorType.Rsi, "RSI (14)"),
            new IndicatorOption(IndicatorType.Stochastic, "Stochastic (14, 3, 3)"),
            new IndicatorOption(IndicatorType.Macd, "MACD (12, 26, 9)"),
        ];

        SelectedIndicator = IndicatorOptions[0];
    }

    partial void OnSelectedThemeChanged(AppTheme value)
        => _themeService.Apply(value);

    partial void OnSymbolsChanged(List<SymbolResponseDto> value)
    {
        FilteredSymbols = CollectionViewSource.GetDefaultView(value);
        FilteredSymbols.Filter = SymbolMatchesFilter;
        FilteredSymbols.Refresh();
        OnPropertyChanged(nameof(FilteredSymbols));
    }

    partial void OnSymbolFilterChanged(string value)
        => FilteredSymbols?.Refresh();

    partial void OnSelectedPositionChanged(PositionResponseDto? value)
    {
        if (value is null)
            return;
        ModifyStopLoss = value.StopLoss > 0 ? value.StopLoss : null;
        ModifyTakeProfit = value.TakeProfit > 0 ? value.TakeProfit : null;
    }

    private bool SymbolMatchesFilter(object obj)
    {
        if (string.IsNullOrWhiteSpace(SymbolFilter))
            return true;

        return obj is SymbolResponseDto s &&
               s.Name.Contains(SymbolFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void WireRealtimeEvents()
    {
        _realtimeService.TickReceived += OnTick;
        _realtimeService.CandleReceived += OnCandle;
        _realtimeService.PositionsReceived += OnPositions;
        _realtimeService.AccountReceived += OnAccount;
        _realtimeService.ConnectionChanged += OnConnectionChanged;
        _accountService.AccountUpdated += OnAccountUpdated;
    }

    private void OnTick(TickUpdateDto tick)
        => OnUiThread(() =>
        {
            if (!string.Equals(tick.Symbol, _activeSymbol, StringComparison.OrdinalIgnoreCase))
                return;
            LastBid = tick.Bid;
            LastAsk = tick.Ask;
        });

    private void OnCandle(CandleUpdateDto candle)
        => OnUiThread(() =>
        {
            CandlesReceived++;
            if (!string.Equals(candle.Symbol, _activeSymbol, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(candle.Timeframe, _activeTimeframe, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _chartService.AddCandle(candle);
        });

    private void OnPositions(List<PositionUpdateDto> positions)
        => OnUiThread(() =>
        {
            var currentTickets = Positions.Select(p => p.Ticket).ToHashSet();
            var incoming = positions.ToDictionary(p => p.Ticket);

            foreach (var ticket in currentTickets)
            {
                if (!incoming.ContainsKey(ticket))
                {
                    _ = LogOperationAsync("positions", "closed", $"Position {ticket} closed.");
                }
            }

            // Remember the highlighted row and which checkboxes were ticked so
            // live updates don't reset the user's selection.
            var selectedTicket = SelectedPosition?.Ticket;
            var checkedTickets = Positions.Where(p => p.IsSelected).Select(p => p.Ticket).ToHashSet();

            Positions = positions.Select(p => new PositionResponseDto
            {
                Ticket = p.Ticket,
                Symbol = p.Symbol,
                Volume = p.Volume,
                Type = p.Type,
                PriceOpen = p.PriceOpen,
                StopLoss = p.StopLoss,
                TakeProfit = p.TakeProfit,
                Profit = p.Profit,
                IsSelected = checkedTickets.Contains(p.Ticket),
            }).ToList();

            if (selectedTicket is not null)
                SelectedPosition = Positions.FirstOrDefault(p => p.Ticket == selectedTicket);
        });

    private void OnAccount(AccountUpdateDto update)
        => OnUiThread(() =>
        {
            if (Account is null)
            {
                _ = LogOperationAsync("account", "update", $"Account {update.Login} {update.Currency} updated.");
            }
            Account = new AccountResponseDto
            {
                Login = update.Login,
                Currency = update.Currency,
                Server = update.Server,
                Leverage = update.Leverage,
                Balance = update.Balance,
                Equity = update.Equity,
                Margin = update.Margin,
                FreeMargin = update.FreeMargin,
                MarginLevel = update.MarginLevel,
                Profit = update.Profit,
                TradeAllowed = update.TradeAllowed,
                TradeMode = Account?.TradeMode ?? string.Empty,
                MarginMode = Account?.MarginMode ?? string.Empty,
                Credit = Account?.Credit ?? 0,
                Connected = true,
            };
        });

    private void OnAccountUpdated(AccountUpdateDto update)
        => OnAccount(update);

    private void OnConnectionChanged(ConnectionStatusDto status)
        => OnUiThread(() =>
        {
            if (status.Connected)
            {
                IsConnected = true;
                Status = "Connected to server.";
            }
        });

    private void OnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        Status = "Connecting...";

        try
        {
            await _realtimeService.ConnectAsync();

            var health = await _marketDataService.HealthAsync();
            IsConnected = health.Connected;
            Status = health.Status;

            Symbols = await _marketDataService.GetSymbolsAsync();

            await LoadAccountAsync();
            await LoadPositionsAsync();

            await _realtimeService.StartPositionsAsync();
            await _realtimeService.StartAccountAsync();

            if (Symbols.Count > 0)
                SelectedSymbol = Symbols[0];

            await LoadCandlesAsync();
            await LoadHistoryAsync();
            await LoadPendingOrdersAsync();
            await LoadWatchlistsAsync();
            await LoadStrategiesAsync();
            await LoadSettingsAsync();

            await LogOperationAsync("app", "startup", "Application initialized.");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Status = $"Failed to connect to server: {ex.Message}";
            await LogErrorAsync("app", "startup", ex.Message, ex.StackTrace);
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task LoadCandlesAsync()
    {
        if (SelectedSymbol is null)
            return;

        IsBusy = true;

        try
        {
            var candles = await _marketDataService.GetCandlesAsync(
                SelectedSymbol.Name, SelectedTimeframe, CandleCount);

            _activeSymbol = SelectedSymbol.Name;
            _activeTimeframe = SelectedTimeframe;

            _realtimeSession.Symbol = _activeSymbol;
            _realtimeSession.Timeframe = _activeTimeframe;

            _chartService.LoadCandles(candles);
            Status = $"Loaded {candles.Count} candles for {_activeSymbol} ({_activeTimeframe}).";

            await _realtimeService.StopTicksAsync();
            await _realtimeService.StopCandlesAsync();
            await _realtimeService.StartTicksAsync(_activeSymbol);
            await _realtimeService.StartCandlesAsync(_activeSymbol, _activeTimeframe);
        }
        catch (Exception ex)
        {
            Status = $"Failed to load candles: {ex.Message}";
            await LogErrorAsync("chart", "load", ex.Message, ex.StackTrace);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BuyAsync()
    {
        if (SelectedSymbol is null)
            return;

        try
        {
            var result = await _tradingService.BuyAsync(new TradeRequestDto
            {
                Symbol = SelectedSymbol.Name,
                Volume = Volume,
                StopLoss = StopLoss,
                TakeProfit = TakeProfit,
            });
            Status = $"Buy order filled (ticket {result.Ticket}).";
            await LogOperationAsync("orders", "buy", $"Buy {SelectedSymbol.Name} {Volume} -> ticket {result.Ticket}");
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Buy failed: {ex.Message}";
            await LogErrorAsync("orders", "buy", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task SellAsync()
    {
        if (SelectedSymbol is null)
            return;

        try
        {
            var result = await _tradingService.SellAsync(new TradeRequestDto
            {
                Symbol = SelectedSymbol.Name,
                Volume = Volume,
                StopLoss = StopLoss,
                TakeProfit = TakeProfit,
            });
            Status = $"Sell order filled (ticket {result.Ticket}).";
            await LogOperationAsync("orders", "sell", $"Sell {SelectedSymbol.Name} {Volume} -> ticket {result.Ticket}");
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Sell failed: {ex.Message}";
            await LogErrorAsync("orders", "sell", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (SelectedPosition is null)
            return;

        try
        {
            await _tradingService.CloseAsync(new ClosePositionRequestDto
            {
                Ticket = SelectedPosition.Ticket
            });
            Status = $"Closed position (ticket {SelectedPosition.Ticket}).";
            await LogOperationAsync("orders", "close", $"Closed position {SelectedPosition.Ticket}");
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Close failed: {ex.Message}";
            await LogErrorAsync("orders", "close", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task CloseSelectedPositionsAsync()
    {
        var toClose = Positions.Where(p => p.IsSelected).ToList();
        if (toClose.Count == 0)
        {
            Status = "No positions selected to close.";
            return;
        }

        int closed = 0;
        foreach (var position in toClose)
        {
            try
            {
                await _tradingService.CloseAsync(new ClosePositionRequestDto
                {
                    Ticket = position.Ticket
                });
                closed++;
                await LogOperationAsync("orders", "close", $"Closed position {position.Ticket}");
            }
            catch (Exception ex)
            {
                Status = $"Failed to close {position.Ticket}: {ex.Message}";
                await LogErrorAsync("orders", "close", ex.Message, ex.StackTrace);
            }
        }

        if (closed > 0)
            Status = $"Closed {closed} of {toClose.Count} selected position(s).";

        await LoadPositionsAsync();
    }

    [RelayCommand]
    private async Task PlacePendingOrderAsync()
    {
        if (SelectedSymbol is null || PendingOrderPrice <= 0)
            return;

        try
        {
            var result = await _tradingService.PlacePendingOrderAsync(new PendingOrderRequestDto
            {
                Symbol = SelectedSymbol.Name,
                Type = SelectedPendingType,
                Volume = PendingOrderVolume,
                Price = PendingOrderPrice,
                StopLoss = PendingStopLoss,
                TakeProfit = PendingTakeProfit,
                Expiration = PendingOrderExpiration,
            });
            Status = $"Pending {SelectedPendingType} placed (ticket {result.Ticket}).";
            await LogOperationAsync("orders", "pending", $"Pending {SelectedPendingType} {SelectedSymbol.Name} -> ticket {result.Ticket}");
            await LoadPendingOrdersAsync();
        }
        catch (Exception ex)
        {
            Status = $"Pending order failed: {ex.Message}";
            await LogErrorAsync("orders", "pending", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task CancelPendingOrderAsync()
    {
        if (SelectedPendingOrder is null)
            return;

        try
        {
            await _tradingService.CancelPendingOrderAsync(SelectedPendingOrder.Ticket);
            Status = $"Cancelled pending order (ticket {SelectedPendingOrder.Ticket}).";
            await LogOperationAsync("orders", "cancel_pending", $"Cancelled pending order {SelectedPendingOrder.Ticket}");
            await LoadPendingOrdersAsync();
        }
        catch (Exception ex)
        {
            Status = $"Cancel failed: {ex.Message}";
            await LogErrorAsync("orders", "cancel_pending", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task ModifyPositionAsync()
    {
        if (SelectedPosition is null)
            return;

        try
        {
            await _tradingService.ModifyPositionAsync(new ModifyPositionRequestDto
            {
                Ticket = SelectedPosition.Ticket,
                StopLoss = ModifyStopLoss,
                TakeProfit = ModifyTakeProfit,
            });
            Status = $"Modified position (ticket {SelectedPosition.Ticket}).";
            await LogOperationAsync("orders", "modify", $"Modified SL/TP on {SelectedPosition.Ticket}");
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Modify failed: {ex.Message}";
            await LogErrorAsync("orders", "modify", ex.Message, ex.StackTrace);
        }
    }

    [RelayCommand]
    private async Task RefreshPositionsAsync()
    {
        try
        {
            await LoadPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load positions: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshPendingOrdersAsync()
    {
        try
        {
            await LoadPendingOrdersAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load pending orders: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        try
        {
            TradeHistory = await _tradingService.GetTradeHistoryAsync(new TradeHistoryRequestDto { Count = 200 });
            Status = $"Loaded {TradeHistory.Count} history entries.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to load history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveWatchlistAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWatchlistName))
            return;

        try
        {
            var saved = await _watchlistService.SaveAsync(new WatchlistRequestDto
            {
                Name = NewWatchlistName.Trim(),
                IsActive = true,
                Symbols = NewWatchlistSymbols
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
            });
            NewWatchlistName = "";
            NewWatchlistSymbols = "";
            Status = $"Saved watchlist '{saved.Name}'.";
            await LoadWatchlistsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Watchlist save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteWatchlistAsync()
    {
        if (SelectedWatchlist is null)
            return;

        try
        {
            await _watchlistService.DeleteAsync(SelectedWatchlist.Id);
            Status = $"Deleted watchlist '{SelectedWatchlist.Name}'.";
            await LoadWatchlistsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Watchlist delete failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveStrategyAsync()
    {
        if (string.IsNullOrWhiteSpace(NewStrategyName))
            return;

        try
        {
            var saved = await _strategyService.SaveAsync(new StrategyRequestDto
            {
                Name = NewStrategyName.Trim(),
                Description = NewStrategyDescription,
                ParametersJson = string.IsNullOrWhiteSpace(NewStrategyParameters) ? "{}" : NewStrategyParameters,
                IsActive = true,
            });
            NewStrategyName = "";
            NewStrategyDescription = "";
            NewStrategyParameters = "{}";
            Status = $"Saved strategy '{saved.Name}'.";
            await LoadStrategiesAsync();
        }
        catch (Exception ex)
        {
            Status = $"Strategy save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteStrategyAsync()
    {
        if (SelectedStrategy is null)
            return;

        try
        {
            await _strategyService.DeleteAsync(SelectedStrategy.Id);
            Status = $"Deleted strategy '{SelectedStrategy.Name}'.";
            await LoadStrategiesAsync();
        }
        catch (Exception ex)
        {
            Status = $"Strategy delete failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleStrategy()
    {
        if (IsStrategyEnabled)
        {
            StopStrategy();
            return;
        }

        if (SelectedSymbol is null)
        {
            Status = "Select a symbol first.";
            return;
        }

        _strategy = new ManualStrategy(_tradingService, _accountService, SelectedSymbol.Name);
        _realtimeService.CandleReceived += _strategy.Feed;
        _realtimeService.TickReceived += _strategy.OnTick;
        IsStrategyEnabled = true;
        Status = $"Strategy started on {SelectedSymbol.Name}. Waiting for a reversal setup...";
    }

    private void StopStrategy()
    {
        if (_strategy is not null)
        {
            _realtimeService.CandleReceived -= _strategy.Feed;
            _realtimeService.TickReceived -= _strategy.OnTick;
        }
        _strategy = null;
        IsStrategyEnabled = false;
        Status = "Strategy stopped.";
    }

    partial void OnSelectedSymbolChanged(SymbolResponseDto? value)
    {
        if (IsStrategyEnabled)
        {
            // Retarget the strategy to the newly selected symbol.
            StopStrategy();
            if (value is not null)
            {
                _strategy = new ManualStrategy(_tradingService, _accountService, value.Name);
                _realtimeService.CandleReceived += _strategy.Feed;
                _realtimeService.TickReceived += _strategy.OnTick;
                IsStrategyEnabled = true;
                Status = $"Strategy started on {value.Name}. Waiting for a reversal setup...";
            }
        }
    }

    partial void OnIsStrategyEnabledChanged(bool value)
        => OnPropertyChanged(nameof(StrategyButtonText));

    [RelayCommand]
    private async Task SaveSettingAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSettingKey))
            return;

        try
        {
            await _settingsService.SetAsync(new SettingRequestDto
            {
                Key = NewSettingKey.Trim(),
                Value = NewSettingValue,
            });
            NewSettingKey = "";
            NewSettingValue = "";
            Status = "Setting saved.";
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Setting save failed: {ex.Message}";
        }
    }

    private async Task LoadAccountAsync()
    {
        try
        {
            Account = await _accountService.GetAccountInfoAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load account: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAccountAsync()
        => await LoadAccountAsync();

    private async Task LoadPositionsAsync()
    {
        try
        {
            Positions = await _tradingService.GetPositionsAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load positions: {ex.Message}";
        }
    }

    private async Task LoadPendingOrdersAsync()
    {
        try
        {
            PendingOrders = await _tradingService.GetPendingOrdersAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load pending orders: {ex.Message}";
        }
    }

    private async Task LoadWatchlistsAsync()
    {
        try
        {
            Watchlists = await _watchlistService.GetAllAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load watchlists: {ex.Message}";
        }
    }

    private async Task LoadStrategiesAsync()
    {
        try
        {
            Strategies = await _strategyService.GetAllAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load strategies: {ex.Message}";
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            Settings = await _settingsService.GetAllAsync();
        }
        catch (Exception ex)
        {
            Status = $"Failed to load settings: {ex.Message}";
        }
    }

    private async Task LogOperationAsync(string component, string action, string message)
    {
        try
        {
            await _logger.LogOperationAsync(component, action, message, Account?.Login);
        }
        catch { /* never fail the caller */ }
    }

    private async Task LogErrorAsync(string component, string action, string message, string? stack)
    {
        try
        {
            await _logger.LogErrorAsync(component, action, message, stack, Account?.Login);
        }
        catch { /* never fail the caller */ }
    }
}