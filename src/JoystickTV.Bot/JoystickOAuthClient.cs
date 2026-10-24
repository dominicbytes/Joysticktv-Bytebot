using System;
using System.Collections.Generic;
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

public sealed class JoystickOAuthClient : IDisposable
{
    public const string Scopes = "chat:read chat:write stream:read";
    private readonly HttpClient client;
    private readonly bool ownsClient;

    public JoystickOAuthClient(HttpClient? client = null)
    {
        ownsClient = client == null;
        this.client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public static string CreateVerifier()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        return Base64Url(bytes);
    }

    public static string CreateChallenge(string verifier)
    {
        using var hash = SHA256.Create();
        return Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
    }

    public static string AuthorizationUrl(JoystickConfig config, string state, string verifier)
    {
        var url = "https://joystick.tv/api/oauth/authorize?response_type=code&client_id=" + Uri.EscapeDataString(config.ClientId)
            + "&scope=" + Uri.EscapeDataString(Scopes) + "&state=" + Uri.EscapeDataString(state)
            + "&redirect_uri=" + Uri.EscapeDataString(config.RedirectUri);
        return config.PublicClient ? url + "&code_challenge_method=S256&code_challenge=" + CreateChallenge(verifier) : url;
    }

    public async Task AuthorizeAsync(JoystickConfig config, CancellationToken cancellationToken)
    {
        var redirect = new Uri(config.RedirectUri);
        if (redirect.Scheme != Uri.UriSchemeHttp || !(redirect.Host == "127.0.0.1" || redirect.Host == "localhost"))
            throw new InvalidOperationException("The Joystick redirect URI must be an HTTP loopback address.");

        var state = CreateVerifier();
        var verifier = CreateVerifier();
        using var listener = new HttpListener();
        listener.Prefixes.Add(config.RedirectUri);
        listener.Start();
        Process.Start(new ProcessStartInfo(AuthorizationUrl(config, state, verifier)) { UseShellExecute = true });
        var callback = listener.GetContextAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var completed = await Task.WhenAny(callback, Task.Delay(Timeout.Infinite, timeout.Token)).ConfigureAwait(false);
        if (completed != callback)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Joystick authorization timed out.");
        }
        var context = await callback.ConfigureAwait(false);
        var code = context.Request.QueryString["code"];
        if (string.IsNullOrEmpty(code) || !string.Equals(context.Request.QueryString["state"], state, StringComparison.Ordinal))
        {
            await WriteResponseAsync(context.Response, "Authorization failed. You can close this window.").ConfigureAwait(false);
            throw new InvalidOperationException("Joystick authorization returned a missing code or invalid state.");
        }
        await WriteResponseAsync(context.Response, "Joystick.TV authorization received. You can close this window.").ConfigureAwait(false);
        await ExchangeCodeAsync(config, code, verifier, cancellationToken).ConfigureAwait(false);
    }

    public Task ExchangeCodeAsync(JoystickConfig config, string code, string verifier, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = config.RedirectUri
        };
        if (config.PublicClient) fields["code_verifier"] = verifier;
        return RequestTokensAsync(config, fields, cancellationToken);
    }

    public Task RefreshAsync(JoystickConfig config, string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(config, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken
        }, cancellationToken);

    private async Task RequestTokensAsync(JoystickConfig config, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.joystick.tv/api/oauth/token");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (config.PublicClient) fields["client_id"] = config.ClientId;
        else request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(config.ClientId + ":" + config.GetClientSecret())));
        request.Content = new FormUrlEncodedContent(fields);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Joystick token request failed (HTTP " + (int)response.StatusCode + "). Run Authorize again.");
        try
        {
            var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var access = json.Value<string>("access_token");
            var refresh = json.Value<string>("refresh_token");
            var seconds = json.Value<long?>("expires_in") ?? 0;
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || seconds <= 60 || seconds > 31536000)
                throw new InvalidOperationException("Joystick returned an incomplete token response. Run Authorize again.");
            config.SetAccessToken(access!);
            config.SetRefreshToken(refresh!);
            config.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            throw new InvalidOperationException("Joystick returned an invalid token response. Run Authorize again.");
        }
    }

    public static DateTimeOffset? JwtExpiresAt(string token)
    {
        // Expiry is only a scheduling hint; Joystick still authenticates the token.
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            var json = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var exp = json.Value<long?>("exp");
            return exp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(exp.Value) : (DateTimeOffset?)null;
        }
        catch (Exception exception) when (exception is FormatException || exception is Newtonsoft.Json.JsonException || exception is ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task WriteResponseAsync(HttpListenerResponse response, string text)
    {
        var bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset=\"utf-8\"><title>JoystickTV.Bot</title><p>" + WebUtility.HtmlEncode(text) + "</p>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        response.Close();
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }
}
