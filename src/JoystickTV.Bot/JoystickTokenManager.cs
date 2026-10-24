using System;
using System.Threading;
using System.Threading.Tasks;

namespace JoystickTV.Bot;

public sealed class JoystickTokenManager
{
    private readonly Func<JoystickConfig> load;
    private readonly Action<JoystickConfig> save;
    private readonly JoystickOAuthClient oauth;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    public JoystickTokenManager(Func<JoystickConfig> load, Action<JoystickConfig> save, JoystickOAuthClient oauth)
    {
        this.load = load;
        this.save = save;
        this.oauth = oauth;
    }

    public void Configure(string clientId, string secret, string redirectUri, string channelId, bool publicClient)
    {
        if (!gate.Wait(0)) throw new InvalidOperationException("Authorization or token refresh is in progress. Try Configure again when it finishes.");
        try
        {
            var config = load();
            var nextSecret = publicClient ? string.Empty : string.IsNullOrWhiteSpace(secret) ? config.GetClientSecret() : secret;
            if (string.IsNullOrWhiteSpace(clientId) || (!publicClient && string.IsNullOrWhiteSpace(nextSecret)))
                throw new InvalidOperationException("A Client ID is required; confidential clients also require a client secret.");
            if (config.ClientId != clientId || config.PublicClient != publicClient || config.RedirectUri != redirectUri || config.GetClientSecret() != nextSecret)
                config.ClearTokens();
            config.ClientId = clientId;
            config.PublicClient = publicClient;
            config.SetClientSecret(nextSecret);
            config.RedirectUri = redirectUri;
            config.ChannelId = channelId;
            save(config);
        }
        finally { gate.Release(); }
    }

    public async Task AuthorizeAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = load();
            config.ClearTokens();
            save(config);
            await oauth.AuthorizeAsync(config, cancellationToken).ConfigureAwait(false);
            save(config);
        }
        finally { gate.Release(); }
    }

    public async Task<JoystickConfig> GetAuthorizedConfigAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = load();
            var access = config.GetAccessToken();
            if (string.IsNullOrWhiteSpace(access)) throw new InvalidOperationException("Run Authorize before Start.");
            config.AccessTokenExpiresAt ??= JoystickOAuthClient.JwtExpiresAt(access);
            if (config.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60)) return config;
            var refresh = config.GetRefreshToken();
            if (string.IsNullOrWhiteSpace(refresh)) throw new InvalidOperationException("Joystick authorization expired. Run Authorize again.");
            cancellationToken.ThrowIfCancellationRequested();
            // A failed/ambiguous refresh must never retry a possibly consumed token.
            config.ClearTokens();
            save(config);
            await oauth.RefreshAsync(config, refresh, cancellationToken).ConfigureAwait(false);
            save(config);
            return config;
        }
        finally { gate.Release(); }
    }

    public async Task InvalidateAccessAsync(string accessToken, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = load();
            if (config.GetAccessToken() != accessToken) return;
            config.AccessTokenExpiresAt = DateTimeOffset.MinValue;
            save(config);
        }
        finally { gate.Release(); }
    }
}
