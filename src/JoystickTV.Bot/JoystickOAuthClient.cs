using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace JoystickTV.Bot;

public sealed class JoystickOAuthClient
{
    public async Task AuthorizeAsync(JoystickConfig config, CancellationToken cancellationToken)
    {
        var redirect = new Uri(config.RedirectUri);
        if (redirect.Scheme != Uri.UriSchemeHttp || !(redirect.Host == "127.0.0.1" || redirect.Host == "localhost"))
        {
            throw new InvalidOperationException("The Joystick redirect URI must be an HTTP loopback address.");
        }

        var stateBytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(stateBytes);
        var state = Convert.ToBase64String(stateBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var authorizeUrl = "https://joystick.tv/api/oauth/authorize?response_type=code&client_id=" + Uri.EscapeDataString(config.ClientId) + "&scope=bot&state=" + Uri.EscapeDataString(state) + "&redirect_uri=" + Uri.EscapeDataString(config.RedirectUri);

        using var listener = new HttpListener();
        listener.Prefixes.Add(config.RedirectUri);
        listener.Start();
        Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

        var contextTask = listener.GetContextAsync();
        var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5), cancellationToken)).ConfigureAwait(false);
        if (completed != contextTask) throw new TimeoutException("Joystick authorization timed out.");
        var context = await contextTask.ConfigureAwait(false);
        NameValueCollection query = context.Request.QueryString;
        var code = query["code"];
        if (string.IsNullOrEmpty(code) || !string.Equals(query["state"], state, StringComparison.Ordinal))
        {
            await WriteResponseAsync(context.Response, "Authorization failed. You can close this window.").ConfigureAwait(false);
            throw new InvalidOperationException("Joystick authorization returned a missing code or invalid state.");
        }

        await WriteResponseAsync(context.Response, "Joystick.TV authorization received. You can close this window.").ConfigureAwait(false);
        using var client = new HttpClient();
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ClientId + ":" + config.GetClientSecret()));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        var tokenUri = "https://api.joystick.tv/api/oauth/token?redirect_uri=" + Uri.EscapeDataString(config.RedirectUri) + "&code=" + Uri.EscapeDataString(code) + "&grant_type=authorization_code";
        using var response = await client.PostAsync(tokenUri, new StringContent(string.Empty), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var tokenJson = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        config.SetAccessToken(tokenJson.Value<string>("access_token") ?? throw new InvalidOperationException("Joystick did not return an access token."));
        config.SetRefreshToken(tokenJson.Value<string>("refresh_token") ?? string.Empty);
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, string text)
    {
        var bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset=\"utf-8\"><title>JoystickTV.Bot</title><p>" + WebUtility.HtmlEncode(text) + "</p>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        response.Close();
    }
}
