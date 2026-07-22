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
    private static JoystickConnection? connection;

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

        if (string.IsNullOrWhiteSpace(clientId) && !TryShowConfiguration(config, out clientId, out clientSecret, out redirectUri, out channelId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(clientId) || (string.IsNullOrWhiteSpace(clientSecret) && string.IsNullOrWhiteSpace(config.ProtectedClientSecret)))
        {
            proxy.LogError("[JoystickTV.Bot] Client ID and client secret are required.");
            return false;
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect) || redirect.Scheme != Uri.UriSchemeHttp || !(redirect.Host == "127.0.0.1" || redirect.Host == "localhost") || !redirectUri.EndsWith("/", StringComparison.Ordinal))
        {
            proxy.LogError("[JoystickTV.Bot] Redirect URI must be an HTTP localhost/127.0.0.1 URL ending in '/'.");
            return false;
        }

        config.ClientId = clientId.Trim();
        if (!string.IsNullOrWhiteSpace(clientSecret)) config.SetClientSecret(clientSecret);
        config.RedirectUri = redirectUri;
        config.ChannelId = channelId.Trim();
        ConfigStore.Save(config);
        proxy.LogInfo("[JoystickTV.Bot] Configuration saved. Secrets are protected with CurrentUser DPAPI.");
        return true;
    }

    public static bool Authorize(IInlineInvokeProxy proxy)
    {
        JoystickConfig config;
        try
        {
            config = ConfigStore.Load();
            if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.GetClientSecret())) throw new InvalidOperationException("Configure credentials first.");
        }
        catch (Exception exception)
        {
            proxy.LogError("[JoystickTV.Bot] Authorization cannot start: " + exception.Message);
            return false;
        }

        Task.Run(async () =>
        {
            try
            {
                await new JoystickOAuthClient().AuthorizeAsync(config, CancellationToken.None).ConfigureAwait(false);
                ConfigStore.Save(config);
                proxy.LogInfo("[JoystickTV.Bot] OAuth authorization completed and tokens were protected with CurrentUser DPAPI.");
            }
            catch (Exception exception)
            {
                proxy.LogError("[JoystickTV.Bot] OAuth authorization failed: " + exception.Message);
            }
        });
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
        try
        {
            var config = ConfigStore.Load();
            configured = !string.IsNullOrWhiteSpace(config.ClientId) && !string.IsNullOrWhiteSpace(config.GetClientSecret());
            authorized = !string.IsNullOrWhiteSpace(config.GetAccessToken());
        }
        catch { }
        var running = false;
        var connected = false;
        lock (Sync) { running = connection?.IsRunning == true; connected = connection?.IsConnected == true; }
        proxy.LogInfo($"[JoystickTV.Bot] Configured={configured}; Authorized={authorized}; Running={running}; Connected={connected}; Credentials=<redacted>");
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
                connection = new JoystickConnection(adapter, adapter);
            }
            return connection;
        }
    }

    private static string GetString(IDictionary<string, object> arguments, string key) => arguments.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static bool TryShowConfiguration(JoystickConfig config, out string clientId, out string clientSecret, out string redirectUri, out string channelId)
    {
        using var form = new Form { Text = "JoystickTV.Bot Configuration", Width = 590, Height = 255, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterScreen };
        var clientIdBox = AddField(form, "Client ID", 16, config.ClientId, false);
        var secretBox = AddField(form, "Client secret", 52, string.Empty, true);
        var redirectBox = AddField(form, "Redirect URI", 88, config.RedirectUri, false);
        var channelBox = AddField(form, "Channel ID", 124, config.ChannelId, false);
        var save = new Button { Left = 388, Top = 166, Width = 82, Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Left = 476, Top = 166, Width = 82, Text = "Cancel", DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { save, cancel });
        form.AcceptButton = save;
        form.CancelButton = cancel;
        var accepted = form.ShowDialog() == DialogResult.OK;
        clientId = clientIdBox.Text.Trim(); clientSecret = secretBox.Text; redirectUri = redirectBox.Text.Trim(); channelId = channelBox.Text.Trim();
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
