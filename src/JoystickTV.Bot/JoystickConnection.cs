using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public sealed class JoystickConnection : IDisposable
{
    internal static readonly string GatewayIdentifier = JsonConvert.SerializeObject(new { channel = "GatewayChannel", event_version = "v2" });
    private readonly object sync = new object();
    private readonly IEventDispatcher dispatcher;
    private readonly IPluginLogger logger;
    private readonly JoystickTokenManager tokens;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private ClientWebSocket? socket;
    private bool connected;
    private string channelId = string.Empty;

    public JoystickConnection(IEventDispatcher dispatcher, IPluginLogger logger, JoystickTokenManager tokens)
    {
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.tokens = tokens;
    }

    public bool IsRunning { get { lock (sync) return worker != null && !worker.IsCompleted && cancellation?.IsCancellationRequested == false; } }
    public bool IsConnected { get { lock (sync) return connected && socket?.State == WebSocketState.Open; } }
    public string ChannelId { get { lock (sync) return channelId; } }

    public void Start(JoystickConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId) || (!config.PublicClient && string.IsNullOrWhiteSpace(config.GetClientSecret())))
            throw new InvalidOperationException("Configure the Joystick client first.");
        if (string.IsNullOrWhiteSpace(config.GetAccessToken()))
            throw new InvalidOperationException("Run Authorize before Start.");

        lock (sync)
        {
            if (worker != null && !worker.IsCompleted && cancellation?.IsCancellationRequested == false) return;
            var previous = worker;
            var source = new CancellationTokenSource();
            cancellation = source;
            channelId = config.ChannelId;
            worker = Task.Run(async () =>
            {
                try
                {
                    if (previous != null)
                    {
                        try { await previous.ConfigureAwait(false); }
                        catch (OperationCanceledException) { }
                    }
                    source.Token.ThrowIfCancellationRequested();
                    await RunAsync(source.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (source.IsCancellationRequested) { }
                finally
                {
                    lock (sync) if (ReferenceEquals(cancellation, source)) cancellation = null;
                    source.Dispose();
                }
            });
        }
        logger.Info("Joystick connection started.");
    }

    public void Stop()
    {
        lock (sync)
        {
            if (worker != null && !worker.IsCompleted) cancellation?.Cancel();
            socket?.Abort();
            connected = false;
        }
        logger.Info("Joystick connection stopped.");
    }

    public async Task SendMessageAsync(string text, string? requestedChannelId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A message is required.", nameof(text));
        ClientWebSocket activeSocket;
        string target;
        lock (sync)
        {
            target = string.IsNullOrWhiteSpace(requestedChannelId) ? channelId : requestedChannelId!;
            if (!connected || socket == null) throw new InvalidOperationException("Joystick is not connected.");
            activeSocket = socket;
        }
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("No Joystick channel ID is known. Configure one or receive an event first.");
        await SendAsync(activeSocket, SendMessagePayload(text, target), cancellationToken).ConfigureAwait(false);
    }

    internal static Uri GatewayUri(string accessToken) =>
        new Uri("wss://api.joystick.tv/cable?token=" + Uri.EscapeDataString(accessToken));

    internal static string SendMessagePayload(string text, string target) =>
        JsonConvert.SerializeObject(new
        {
            command = "message", identifier = GatewayIdentifier,
            data = JsonConvert.SerializeObject(new { action = "send_message", text, channel_id = target })
        });

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var config = await tokens.GetAuthorizedConfigAsync(cancellationToken).ConfigureAwait(false);
                await ConnectAndReadAsync(config, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (InvalidOperationException exception) { logger.Error(exception.Message); return; }
            catch (Exception exception) { logger.Error("Joystick connection failed (" + exception.GetType().Name + "). Retrying."); }
            try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }
    }

    private async Task ConnectAndReadAsync(JoystickConfig config, CancellationToken cancellationToken)
    {
        var accessToken = config.GetAccessToken();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = (config.AccessTokenExpiresAt!.Value - DateTimeOffset.UtcNow).Subtract(TimeSpan.FromSeconds(60));
        if (remaining <= TimeSpan.Zero) return;
        // Reconnect before expiry, even if the server keeps an old socket alive.
        session.CancelAfter(remaining);
        using var activeSocket = new ClientWebSocket();
        activeSocket.Options.AddSubProtocol("actioncable-v1-json");
        try
        {
            await activeSocket.ConnectAsync(GatewayUri(accessToken), session.Token).ConfigureAwait(false);
            lock (sync) socket = activeSocket;
            var subscribe = JsonConvert.SerializeObject(new { command = "subscribe", identifier = GatewayIdentifier });
            await SendAsync(activeSocket, subscribe, session.Token).ConfigureAwait(false);
            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            while (activeSocket.State == WebSocketState.Open && !session.IsCancellationRequested)
            {
                var result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), session.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if ((int?)result.CloseStatus == 4401)
                        await tokens.InvalidateAccessAsync(accessToken, cancellationToken).ConfigureAwait(false);
                    break;
                }
                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage) continue;
                var json = Encoding.UTF8.GetString(message.ToArray());
                message.SetLength(0);
                var envelope = JObject.Parse(json);
                if (envelope.Value<string>("type") == "reject_subscription")
                    throw new InvalidOperationException("Joystick rejected the subscription. Check the bot scopes and run Authorize again.");
                if (envelope.Value<string>("type") == "confirm_subscription")
                {
                    lock (sync) connected = true;
                    logger.Info("Connected to Joystick.TV GatewayChannel (v2).");
                }
                var platformEvent = JoystickParser.ParseCableMessage(json);
                if (platformEvent == null) continue;
                if (platformEvent.Arguments.TryGetValue("channelId", out var observed) && !string.IsNullOrWhiteSpace(Convert.ToString(observed)))
                {
                    lock (sync) channelId = Convert.ToString(observed)!;
                }
                dispatcher.Dispatch(platformEvent);
            }
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(socket, activeSocket)) { socket = null; connected = false; }
            }
        }
    }

    private async Task SendAsync(ClientWebSocket activeSocket, string payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await activeSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false); }
        finally { sendLock.Release(); }
    }

    public void Dispose()
    {
        Stop();
        Task? pending;
        lock (sync) pending = worker;
        if (pending == null) sendLock.Dispose();
        else _ = pending.ContinueWith(_ => sendLock.Dispose(), TaskScheduler.Default);
    }
}
