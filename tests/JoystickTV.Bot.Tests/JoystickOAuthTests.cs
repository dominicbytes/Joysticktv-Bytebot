using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JoystickTV.Bot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JoystickTV.Bot.Tests;

public sealed class JoystickOAuthTests
{
    [Fact]
    public void Pkce_UsesRfc7636S256Vector()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", JoystickOAuthClient.CreateChallenge(verifier));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", JoystickOAuthClient.CreateVerifier());
        var config = new JoystickConfig { ClientId = "id", PublicClient = true };
        var url = JoystickOAuthClient.AuthorizationUrl(config, "state", verifier);
        Assert.Contains("scope=chat%3Aread%20chat%3Awrite%20stream%3Aread", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.DoesNotContain(verifier, url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodeExchange_UsesFormAndCorrectClientAuthentication(bool publicClient)
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var oauth = new JoystickOAuthClient(http);
        var config = new JoystickConfig { ClientId = "test-id", PublicClient = publicClient };
        config.SetClientSecret("fake-secret");
        await oauth.ExchangeCodeAsync(config, "code+value", "verifier", CancellationToken.None);
        Assert.Equal("https://api.joystick.tv/api/oauth/token", handler.Url);
        Assert.Equal("application/x-www-form-urlencoded", handler.ContentType);
        Assert.Contains("grant_type=authorization_code", handler.Body);
        Assert.Contains("code=code%2Bvalue", handler.Body);
        Assert.Contains("redirect_uri=", handler.Body);
        if (publicClient)
        {
            Assert.Null(handler.Authorization);
            Assert.Contains("client_id=test-id", handler.Body);
            Assert.Contains("code_verifier=verifier", handler.Body);
        }
        else
        {
            Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-id:fake-secret")), handler.Authorization);
            Assert.DoesNotContain("code_verifier", handler.Body);
        }
        Assert.Equal("new-access", config.GetAccessToken());
        Assert.Equal("new-refresh", config.GetRefreshToken());
        Assert.True(config.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddHours(3));
        Assert.DoesNotContain("new-access", JsonConvert.SerializeObject(config));
        Assert.DoesNotContain("new-refresh", JsonConvert.SerializeObject(config));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRefresh_PersistsNewestPairAndRefreshesOnlyOnce(bool publicClient)
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var oauth = new JoystickOAuthClient(http);
        var config = ExpiredConfig();
        config.PublicClient = publicClient;
        config.SetClientSecret("fake-secret");
        var saves = new List<string>();
        var manager = new JoystickTokenManager(
            () => Clone(config), value => { config = Clone(value); saves.Add(value.GetRefreshToken()); }, oauth);
        var tasks = new List<Task<JoystickConfig>>();
        for (var index = 0; index < 8; index++) tasks.Add(manager.GetAuthorizedConfigAsync(CancellationToken.None));
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, handler.Requests);
        Assert.Contains("grant_type=refresh_token", handler.Body);
        Assert.Contains("refresh_token=old-refresh", handler.Body);
        if (publicClient) Assert.Null(handler.Authorization);
        else Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-id:fake-secret")), handler.Authorization);
        Assert.Equal(new[] { "", "new-refresh" }, saves);
        foreach (var result in results) Assert.Equal("new-access", result.GetAccessToken());
        Assert.Equal("owner-channel", config.ChannelId);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("network")]
    [InlineData("invalid")]
    public async Task FailedRefresh_DoesNotReusePossiblyConsumedToken(string failure)
    {
        using var handler = new TokenHandler { Failure = failure };
        using var http = new HttpClient(handler);
        using var oauth = new JoystickOAuthClient(http);
        var config = ExpiredConfig();
        var manager = new JoystickTokenManager(() => Clone(config), value => config = Clone(value), oauth);
        await Assert.ThrowsAnyAsync<Exception>(() => manager.GetAuthorizedConfigAsync(CancellationToken.None));
        Assert.Empty(config.GetAccessToken());
        Assert.Empty(config.GetRefreshToken());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.GetAuthorizedConfigAsync(CancellationToken.None));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task LegacyJwt_InfersExpiryWithoutUsingClientSecretAsGatewayToken()
    {
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var oauth = new JoystickOAuthClient(http);
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":" + DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds() + "}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var config = new JoystickConfig { ClientId = "legacy", ChannelId = "owner" };
        config.SetClientSecret("fake-secret");
        config.SetAccessToken("header." + payload + ".signature");
        var manager = new JoystickTokenManager(() => Clone(config), value => config = Clone(value), oauth);
        var result = await manager.GetAuthorizedConfigAsync(CancellationToken.None);
        Assert.Equal(0, handler.Requests);
        Assert.True(result.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(result.GetAccessToken(), Uri.UnescapeDataString(JoystickConnection.GatewayUri(result.GetAccessToken()).Query.Substring(7)));
    }

    [Fact]
    public void Configuration_ChangingClientClearsTokensButChannelChangePreservesThem()
    {
        using var oauth = new JoystickOAuthClient();
        var config = ExpiredConfig();
        var manager = new JoystickTokenManager(() => Clone(config), value => config = Clone(value), oauth);
        manager.Configure(config.ClientId, "", config.RedirectUri, "new-channel", true);
        Assert.Equal("old-access", config.GetAccessToken());
        manager.Configure("different-client", "", config.RedirectUri, "new-channel", true);
        Assert.Empty(config.GetAccessToken());
        Assert.Empty(config.GetRefreshToken());
    }

    [Fact]
    public void GatewayCommand_UsesSameV2IdentifierAndSnakeCaseChannel()
    {
        var request = JObject.Parse(JoystickConnection.SendMessagePayload("Hello \"viewer\"", "owner"));
        Assert.Equal(JoystickConnection.GatewayIdentifier, request.Value<string>("identifier"));
        Assert.Equal("v2", JObject.Parse((string)request["identifier"]!).Value<string>("event_version"));
        var data = JObject.Parse((string)request["data"]!);
        Assert.Equal("send_message", data.Value<string>("action"));
        Assert.Equal("owner", data.Value<string>("channel_id"));
        Assert.Null(data["channelId"]);
        Assert.Equal("Hello \"viewer\"", data.Value<string>("text"));
    }

    private static JoystickConfig ExpiredConfig()
    {
        var config = new JoystickConfig { ClientId = "test-id", PublicClient = true, ChannelId = "owner-channel", AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        config.SetAccessToken("old-access");
        config.SetRefreshToken("old-refresh");
        return config;
    }

    private static JoystickConfig Clone(JoystickConfig config) =>
        JsonConvert.DeserializeObject<JoystickConfig>(JsonConvert.SerializeObject(config))!;

    private sealed class TokenHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? Url { get; private set; }
        public string? ContentType { get; private set; }
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string Failure { get; set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Url = request.RequestUri!.AbsoluteUri;
            ContentType = request.Content!.Headers.ContentType!.MediaType;
            Body = await request.Content.ReadAsStringAsync();
            Authorization = request.Headers.Authorization?.ToString();
            await Task.Delay(20, cancellationToken);
            if (Failure == "network") throw new HttpRequestException("ambiguous network failure");
            return new HttpResponseMessage(Failure == "server" ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(Failure == "invalid" ? "not-json" : "{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":14400}")
            };
        }
    }
}
