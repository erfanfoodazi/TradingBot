using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Json;
using Trading.Infrastructure.Options;
using Trading.Shared.Enums;
using Trading.Shared.Events;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Maintains lightweight WebSocket connections to the Python backend for
/// ticks, candles, positions and account updates. Parses server payloads and
/// raises strongly typed events for the presentation layer to consume.
/// </summary>
public class RealtimeService : IRealtimeService, IDisposable
{
    private static readonly JsonSerializerOptions JsonReadOptions = CreateJsonOptions();

    private sealed class Channel
    {
        public string Key;
        public string Path;
        public ClientWebSocket? Socket;
        public CancellationTokenSource? Cts;
        public Task? LoopTask;

        public Channel(string key, string path)
        {
            Key = key;
            Path = path;
        }
    }

    private readonly string _wsUri;
    private readonly string? _apiKey;
    private readonly int _reconnectDelayMs;
    private readonly ILogger<RealtimeService> _logger;
    private readonly object _channelsGate = new();
    private readonly Dictionary<string, Channel> _channels = new();
    private CancellationTokenSource? _lifetimeCts;

    public event Action<TickUpdateDto>? TickReceived;
    public event Action<CandleUpdateDto>? CandleReceived;
    public event Action<List<PositionUpdateDto>>? PositionsReceived;
    public event Action<AccountUpdateDto>? AccountReceived;
    public event Action<ConnectionStatusDto>? ConnectionChanged;

    public RealtimeService(
        IOptions<PythonApiOptions> options,
        ILogger<RealtimeService> logger)
    {
        _wsUri = options.Value.WebSocketUrl;
        _apiKey = options.Value.ApiKey;
        _reconnectDelayMs = options.Value.ReconnectDelayMs;
        _logger = logger;
        _lifetimeCts = new CancellationTokenSource();
    }

    public bool IsConnected
    {
        get
        {
            lock (_channelsGate)
                return _channels.Values.Any(c => c.Socket?.State == WebSocketState.Open);
        }
    }

    public Task ConnectAsync()
    {
        if (_lifetimeCts is null || _lifetimeCts.IsCancellationRequested)
            _lifetimeCts = new CancellationTokenSource();
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
        => await StopAllAsync();

    public Task StartTicksAsync(string symbol)
        => OpenChannelAsync("tick", $"/ws/price/{symbol}");

    public async Task StopTicksAsync()
        => await CloseChannelAsync("tick");

    public Task StartCandlesAsync(string symbol, string timeframe)
        => OpenChannelAsync("candle", $"/ws/candles/{symbol}/{timeframe}");

    public async Task StopCandlesAsync()
        => await CloseChannelAsync("candle");

    public Task StartPositionsAsync()
        => OpenChannelAsync("positions", "/ws/positions");

    public async Task StopPositionsAsync()
        => await CloseChannelAsync("positions");

    public Task StartAccountAsync()
        => OpenChannelAsync("account", "/ws/account");

    public async Task StopAccountAsync()
        => await CloseChannelAsync("account");

    public async Task StopAllAsync()
    {
        string[] keys;
        lock (_channelsGate)
            keys = _channels.Keys.ToArray();

        foreach (var key in keys)
            await CloseChannelAsync(key);
    }

    private async Task OpenChannelAsync(string key, string path)
    {
        _lifetimeCts ??= new CancellationTokenSource();

        // Idempotent: if a live channel loop already exists under this key, keep
        // it (it self-heals through RunChannelLoopAsync). The worker and the
        // ViewModel may call Start* concurrently, and we must not tear down a
        // healthy connection.
        lock (_channelsGate)
        {
            if (_channels.TryGetValue(key, out var existing) &&
                existing.LoopTask is not null &&
                !existing.LoopTask.IsCompleted)
            {
                return;
            }
        }

        var channel = new Channel(key, path);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        channel.Cts = cts;

        lock (_channelsGate)
        {
            // Another caller may have opened it while we built ours; drop ours.
            if (_channels.TryGetValue(key, out var existing) &&
                existing.LoopTask is not null &&
                !existing.LoopTask.IsCompleted)
            {
                cts.Cancel();
                cts.Dispose();
                return;
            }

            _channels[key] = channel;
            channel.LoopTask = Task.Run(() => RunChannelLoopAsync(channel), CancellationToken.None);
        }

        await Task.CompletedTask;
    }

    private async Task CloseChannelAsync(string key)
    {
        Channel channel;
        lock (_channelsGate)
        {
            if (!_channels.Remove(key, out channel!))
                return;
        }

        if (channel.Cts is not null)
        {
            channel.Cts.Cancel();
            channel.Cts.Dispose();
        }

        if (channel.Socket is not null)
        {
            try
            {
                await channel.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch { /* already closed */ }
            channel.Socket.Dispose();
        }

        if (channel.LoopTask is not null)
        {
            try
            {
                await channel.LoopTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch { /* task already faults on cancellation or is still winding down */ }
        }
    }

    private async Task RunChannelLoopAsync(Channel channel)
    {
        var token = channel.Cts!.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(channel, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Channel {Key} disconnected; will retry.", channel.Key);
                RaiseConnectionChanged(connected: false, channel.Key);
            }

            if (token.IsCancellationRequested)
                break;

            try
            {
                await Task.Delay(_reconnectDelayMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndListenAsync(Channel channel, CancellationToken ct)
    {
        channel.Socket = new ClientWebSocket();
        channel.Socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        var uri = BuildUri(channel.Path);
        await channel.Socket.ConnectAsync(uri, ct);

        RaiseConnectionChanged(connected: true, channel.Key);
        _logger.LogInformation("WebSocket connected: {Path}", channel.Path);

        var buffer = new byte[65536];
        while (channel.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var message = await ReceiveStringAsync(channel.Socket, buffer, ct);
            if (string.IsNullOrWhiteSpace(message))
                continue;

            HandleMessage(channel.Key, message);
        }
    }

    private void HandleMessage(string channelKey, string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);

            switch (channelKey)
            {
                case "tick":
                    TickReceived?.Invoke(doc.RootElement.Deserialize<TickUpdateDto>(JsonReadOptions)!);
                    break;

                case "candle":
                    CandleReceived?.Invoke(doc.RootElement.Deserialize<CandleUpdateDto>(JsonReadOptions)!);
                    break;

                case "positions":
                    if (doc.RootElement.TryGetProperty("positions", out var positionsProp))
                    {
                        PositionsReceived?.Invoke(
                            positionsProp.Deserialize<List<PositionUpdateDto>>(JsonReadOptions) ?? []);
                    }
                    break;

                case "account":
                    AccountReceived?.Invoke(
                        doc.RootElement.Deserialize<AccountUpdateDto>(JsonReadOptions)!);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WebSocket message on channel {Key}.", channelKey);
        }
    }

    private static async Task<string> ReceiveStringAsync(
        WebSocket socket, byte[] buffer, CancellationToken ct)
    {
        var sb = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Text)
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            else if (result.MessageType == WebSocketMessageType.Close)
                return string.Empty;
        }
        while (!result.EndOfMessage);
        return sb.ToString();
    }

    private Uri BuildUri(string path)
    {
        var uri = new Uri($"{_wsUri.TrimEnd('/')}{path}");
        if (string.IsNullOrWhiteSpace(_apiKey))
            return uri;
        return new UriBuilder(uri) { Query = "api_key=" + Uri.EscapeDataString(_apiKey) }.Uri;
    }

    private void RaiseConnectionChanged(bool connected, string channelKey)
    {
        ConnectionChanged?.Invoke(new ConnectionStatusDto
        {
            Connected = connected,
            Time = DateTime.UtcNow,
            Message = connected ? $"Connected ({channelKey})" : $"Disconnected ({channelKey})",
        });
    }

    public void Dispose()
    {
        if (_lifetimeCts is not null)
        {
            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            _lifetimeCts = null;
        }

        List<ClientWebSocket?> sockets;
        lock (_channelsGate)
        {
            sockets = _channels.Values.Select(c => c.Socket).ToList();
            _channels.Clear();
        }

        foreach (var socket in sockets)
            socket?.Dispose();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new FlexibleJsonEnumConverter<PendingOrderType>());
        options.Converters.Add(new FlexibleJsonEnumConverter<OrderState>());
        options.Converters.Add(new FlexibleJsonEnumConverter<DealType>());
        options.Converters.Add(new UnixTimestampConverter());
        return options;
    }
}