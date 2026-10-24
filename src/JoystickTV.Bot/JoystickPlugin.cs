using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Streamer.bot.Plugin.Interface;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public static class JoystickPlugin
{
    private static readonly object Sync = new object();
    private static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamerBot.PlatformBridge", "JoystickTV.Bot");
    private static readonly JsonFileStore<JoystickConfig> ConfigStore = new JsonFileStore<JoystickConfig>(Path.Combine(DataDirectory, "config.json"));
    private static readonly JoystickTokenManager Tokens = new JoystickTokenManager(ConfigStore.Load, ConfigStore.Save, new JoystickOAuthClient());
    private static JoystickConnection? connection;
    private static Task? authorization;

    public static void Initialize(IInlineInvokeProxy proxy)
    {
        foreach (var eventName in JoystickEventNames.All)
        {
            proxy.RegisterCustomTrigger(eventName, eventName, new[] { "JoystickTV.Bot" });
        }
        EnsureConnection(proxy);
        proxy.LogInfo("[JoystickTV.Bot] Initialized. Use Configure, then Authorize, then Start.");
    }

    public static bool Configure(IInlineInvokeProxy proxy, IDictionary<string, object> arguments)
    {
        var config = ConfigStore.Load();
        var clientId = GetString(arguments, "joystickClientId");
        var clientSecret = GetString(arguments, "joystickClientSecret");
        var redirectUri = GetString(arguments, "joystickRedirectUri");
        var channelId = GetString(arguments, "joystickChannelId");
        var publicClient = config.PublicClient;
        if (arguments.ContainsKey("joystickPublicClient") && !bool.TryParse(GetString(arguments, "joystickPublicClient"), out publicClient))
        {
            proxy.LogError("[JoystickTV.Bot] joystickPublicClient must be True or False.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientId) && !TryShowConfiguration(config, out clientId, out clientSecret, out redirectUri, out channelId, out publicClient))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientId) || (!publicClient && string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(config.ProtectedClientSecret)))
        {
            proxy.LogError("[JoystickTV.Bot] Client ID is required; confidential clients also need a client secret.");
            return false;
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect) || redirect.Scheme != Uri.UriSchemeHttp || !(redirect.Host == "127.0.0.1" || redirect.Host == "localhost") || !redirectUri.EndsWith("/", StringComparison.Ordinal))
        {
            proxy.LogError("[JoystickTV.Bot] Redirect URI must be an HTTP localhost/127.0.0.1 URL ending in '/'.");
            return false;
        }

        try
        {
            Stop(proxy);
            Tokens.Configure(clientId.Trim(), clientSecret, redirectUri, channelId.Trim(), publicClient);
        }
        catch (InvalidOperationException exception)
        {
            proxy.LogError("[JoystickTV.Bot] Configure failed: " + exception.Message);
            return false;
        }
        proxy.LogInfo("[JoystickTV.Bot] Configuration saved. Secrets are protected with CurrentUser DPAPI.");
        return true;
    }

    public static bool Authorize(IInlineInvokeProxy proxy)
    {
        JoystickConfig config;
        try
        {
            config = ConfigStore.Load();
            if (string.IsNullOrWhiteSpace(config.ClientId) || (!config.PublicClient && string.IsNullOrWhiteSpace(config.GetClientSecret()))) throw new InvalidOperationException("Configure credentials first.");
        }
        catch (Exception exception)
        {
            proxy.LogError("[JoystickTV.Bot] Authorization cannot start: " + exception.Message);
            return false;
        }

        lock (Sync)
        {
            if (authorization?.IsCompleted == false)
            {
                proxy.LogInfo("[JoystickTV.Bot] Authorization is already waiting for browser approval.");
                return false;
            }
            connection?.Stop();
            authorization = Task.Run(async () =>
            {
                try
                {
                    await Tokens.AuthorizeAsync(CancellationToken.None).ConfigureAwait(false);
                    proxy.LogInfo("[JoystickTV.Bot] OAuth authorization completed and tokens were protected with CurrentUser DPAPI.");
                }
                catch (Exception exception)
                {
                    proxy.LogError("[JoystickTV.Bot] OAuth authorization failed (" + exception.GetType().Name + "). Check the client setup and run Authorize again.");
                }
            });
        }
        proxy.LogInfo("[JoystickTV.Bot] Opening the Joystick.TV authorization page.");
        return true;
    }

    public static bool Start(IInlineInvokeProxy proxy)
    {
        try { EnsureConnection(proxy).Start(ConfigStore.Load()); return true; }
        catch (Exception exception) { proxy.LogError("[JoystickTV.Bot] Start failed: " + exception.Message); return false; }
    }

    public static bool Stop(IInlineInvokeProxy proxy)
    {
        lock (Sync) connection?.Stop();
        return true;
    }

    public static bool Reconnect(IInlineInvokeProxy proxy) { Stop(proxy); return Start(proxy); }

    public static bool Status(IInlineInvokeProxy proxy)
    {
        var configured = false;
        var authorized = false;
        DateTimeOffset? expiresAt = null;
        try
        {
            var config = ConfigStore.Load();
            configured = !string.IsNullOrWhiteSpace(config.ClientId) && (config.PublicClient || !string.IsNullOrWhiteSpace(config.GetClientSecret()));
            authorized = !string.IsNullOrWhiteSpace(config.GetAccessToken());
            expiresAt = config.AccessTokenExpiresAt ?? JoystickOAuthClient.JwtExpiresAt(config.GetAccessToken());
        }
        catch { }
        var running = false;
        var connected = false;
        lock (Sync) { running = connection?.IsRunning == true; connected = connection?.IsConnected == true; }
        proxy.LogInfo($"[JoystickTV.Bot] Configured={configured}; Authorized={authorized}; Running={running}; Connected={connected}; TokenExpiresAt={expiresAt?.ToString("O") ?? "Unknown"}; Credentials=<redacted>");
        return configured && running && connected;
    }

    public static bool SendMessage(IInlineInvokeProxy proxy, IDictionary<string, object> arguments)
    {
        var text = GetString(arguments, "message");
        var channelId = GetString(arguments, "channelId");
        var active = EnsureConnection(proxy);
        Task.Run(async () =>
        {
            try { await active.SendMessageAsync(text, channelId, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception exception) { proxy.LogError("[JoystickTV.Bot] Send Message failed: " + exception.Message); }
        });
        return !string.IsNullOrWhiteSpace(text);
    }

    public static bool Test(IInlineInvokeProxy proxy)
    {
        proxy.TriggerCodeEvent(JoystickEventNames.ChatMessage, new Dictionary<string, object>
        {
            ["platform"] = "joystick", ["eventType"] = "chat.message", ["messageId"] = "test",
            ["channelId"] = "test", ["userName"] = "JoystickBotTest", ["displayName"] = "JoystickBotTest",
            ["message"] = "JoystickTV.Bot combined-chat test", ["userColor"] = "#20c7c7",
            ["badgesJson"] = "[\"streamer\"]", ["emotesJson"] = "[]", ["command"] = string.Empty,
            ["commandArg"] = string.Empty, ["botCommand"] = string.Empty, ["botCommandArg"] = string.Empty,
            ["isStreamer"] = true, ["isModerator"] = false, ["isSubscriber"] = false,
            ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["rawJson"] = "{\"test\":true}"
        });
        proxy.TriggerCodeEvent(JoystickEventNames.Tipped, new Dictionary<string, object>
        {
            ["platform"] = "joystick", ["eventType"] = "stream.event", ["streamEventType"] = "Tipped",
            ["channelId"] = "test", ["userName"] = "JoystickBotTest", ["amountTokens"] = 1L,
            ["tipMenuItem"] = "JoystickTV.Bot test", ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"), ["rawJson"] = "{\"test\":true}"
        });
        return true;
    }

    private static JoystickConnection EnsureConnection(IInlineInvokeProxy proxy)
    {
        lock (Sync)
        {
            if (connection == null)
            {
                var adapter = new StreamerBotAdapter(proxy);
                connection = new JoystickConnection(adapter, adapter, Tokens);
            }
            return connection;
        }
    }

    private static string GetString(IDictionary<string, object> arguments, string key) => arguments.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static bool TryShowConfiguration(JoystickConfig config, out string clientId, out string clientSecret, out string redirectUri, out string channelId, out bool publicClient)
    {
        using var form = new Form { Text = "JoystickTV.Bot Configuration", Width = 590, Height = 290, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen };
        var publicBox = new CheckBox { Left = 126, Top = 12, Width = 400, Text = "Public OAuth client (PKCE, no secret)", Checked = config.PublicClient };
        var clientIdBox = AddField(form, "Client ID", 48, config.ClientId, false);
        var secretBox = AddField(form, "Client secret", 84, string.Empty, true);
        secretBox.Enabled = !publicBox.Checked;
        publicBox.CheckedChanged += (_, __) => secretBox.Enabled = !publicBox.Checked;
        var redirectBox = AddField(form, "Redirect URI", 120, config.RedirectUri, false);
        var channelBox = AddField(form, "Channel ID", 156, config.ChannelId, false);
        var save = new Button { Left = 388, Top = 202, Width = 82, Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Left = 476, Top = 202, Width = 82, Text = "Cancel", DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { publicBox, save, cancel });
        form.AcceptButton = save;
        form.CancelButton = cancel;
        var accepted = form.ShowDialog() == DialogResult.OK;
        clientId = clientIdBox.Text.Trim(); clientSecret = secretBox.Text; redirectUri = redirectBox.Text.Trim(); channelId = channelBox.Text.Trim();
        publicClient = publicBox.Checked;
        return accepted;
    }

    private static TextBox AddField(Form form, string label, int top, string value, bool secret)
    {
        form.Controls.Add(new Label { Left = 12, Top = top + 4, Width = 110, Text = label });
        var box = new TextBox { Left = 126, Top = top, Width = 432, Text = value, UseSystemPasswordChar = secret };
        form.Controls.Add(box);
        return box;
    }
}
