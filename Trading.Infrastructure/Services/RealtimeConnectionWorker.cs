using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Options;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Background worker that keeps the connection to the Python backend alive.
///
/// Why this exists: opening the WebSocket channels only happens from the
/// ViewModel's one-shot startup sequence. If the Python API is not reachable at
/// that exact moment, the channels are never started again and the app stays
/// disconnected forever. This worker polls the backend /health endpoint and
/// (re)establishes the desired realtime channels whenever the backend becomes
/// available, until told to stop.
/// </summary>
public sealed class RealtimeConnectionWorker : BackgroundService
{
    private readonly IRealtimeService _realtime;
    private readonly IMarketDataService _market;
    private readonly IRealtimeSession _session;
    private readonly PythonApiOptions _options;
    private readonly ILogger<RealtimeConnectionWorker> _logger;

    public RealtimeConnectionWorker(
        IRealtimeService realtime,
        IMarketDataService market,
        IRealtimeSession session,
        IOptions<PythonApiOptions> options,
        ILogger<RealtimeConnectionWorker> logger)
    {
        _realtime = realtime;
        _market = market;
        _session = session;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Realtime connection worker started.");

        // Give the process a moment to start so we do not hammer the backend.
        try
        {
            await Task.Delay(_options.ReconnectDelayMs, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Realtime connection attempt failed; will retry.");
            }

            try
            {
                await Task.Delay(_options.ReconnectDelayMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Realtime connection worker stopped.");
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_realtime.IsConnected)
            return;

        var health = await _market.HealthAsync();
        if (!health.Connected)
        {
            _logger.LogDebug("Python API not healthy yet; waiting for next probe.");
            return;
        }

        _logger.LogInformation("Python API is healthy; opening realtime channels.");

        await _realtime.ConnectAsync();

        // Symbol-independent streams.
        await _realtime.StartPositionsAsync();
        await _realtime.StartAccountAsync();

        // The active symbol/timeframe are published by the UI through the
        // session holder. If nothing has been selected yet, skip them; the UI
        // will still work for positions/account.
        if (!string.IsNullOrWhiteSpace(_session.Symbol))
        {
            await _realtime.StartTicksAsync(_session.Symbol);
            await _realtime.StartCandlesAsync(_session.Symbol, _session.Timeframe);
        }
    }
}