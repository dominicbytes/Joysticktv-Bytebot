using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public sealed class JoystickConnection : IDisposable
{
    private readonly object sync = new object();
    private readonly IEventDispatcher dispatcher;
    private readonly IPluginLogger logger;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private ClientWebSocket? socket;
    private string channelId = string.Empty;

    public JoystickConnection(IEventDispatcher dispatcher, IPluginLogger logger)
    {
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    public bool IsRunning { get { lock (sync) return worker != null && !worker.IsCompleted; } }
    public bool IsConnected { get { lock (sync) return socket?.State == WebSocketState.Open; } }
    public string ChannelId => channelId;

    public void Start(JoystickConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.GetClientSecret()))
        {
            throw new InvalidOperationException("Configure the Joystick client ID and client secret first.");
        }

        lock (sync)
        {
            if (worker != null && !worker.IsCompleted) return;
            channelId = config.ChannelId;
            cancellation = new CancellationTokenSource();
            worker = RunAsync(config.ClientId, config.GetClientSecret(), cancellation.Token);
        }
        logger.Info("Joystick connection started.");
    }

    public void Stop()
    {
        CancellationTokenSource? source;
        lock (sync)
        {
            source = cancellation;
            cancellation = null;
            worker = null;
        }
        source?.Cancel();
        source?.Dispose();
        logger.Info("Joystick connection stopped.");
    }

    public async Task SendMessageAsync(string text, string? requestedChannelId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A message is required.", nameof(text));
        var target = string.IsNullOrWhiteSpace(requestedChannelId) ? channelId : requestedChannelId;
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("No Joystick channel ID is known. Configure one or receive an event first.");

        ClientWebSocket activeSocket;
        lock (sync)
        {
            activeSocket = socket ?? throw new InvalidOperationException("Joystick is not connected.");
        }
        if (activeSocket.State != WebSocketState.Open) throw new InvalidOperationException("Joystick is not connected.");

        var data = JsonConvert.SerializeObject(new { action = "send_message", text, channelId = target });
        var payload = JsonConvert.SerializeObject(new { command = "message", identifier = JsonConvert.SerializeObject(new { channel = "GatewayChannel" }), data });
        await SendAsync(activeSocket, payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(clientId, clientSecret, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.Error("Joystick connection failed: " + exception.Message); }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }
    }

    private async Task ConnectAndReadAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var basicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
        using var activeSocket = new ClientWebSocket();
        activeSocket.Options.AddSubProtocol("actioncable-v1-json");
        var uri = new Uri("wss://api.joystick.tv/cable?token=" + Uri.EscapeDataString(basicKey));
        await activeSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
        lock (sync) socket = activeSocket;
        logger.Info("Connected to Joystick.TV GatewayChannel.");

        var subscribe = JsonConvert.SerializeObject(new { command = "subscribe", identifier = JsonConvert.SerializeObject(new { channel = "GatewayChannel" }) });
        await SendAsync(activeSocket, subscribe, cancellationToken).ConfigureAwait(false);

        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (activeSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) break;
            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(message.ToArray());
            message.SetLength(0);
            var platformEvent = JoystickParser.ParseCableMessage(json);
            if (platformEvent != null)
            {
                if (platformEvent.Arguments.TryGetValue("channelId", out var observed) && !string.IsNullOrWhiteSpace(Convert.ToString(observed)))
                {
                    channelId = Convert.ToString(observed) ?? channelId;
                }
                dispatcher.Dispatch(platformEvent);
            }
        }

        lock (sync) if (ReferenceEquals(socket, activeSocket)) socket = null;
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
        sendLock.Dispose();
    }
}
