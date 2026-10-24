using System;
using StreamerBot.PlatformBridge.Core;

namespace JoystickTV.Bot;

public sealed class JoystickConfig
{
    public string ClientId { get; set; } = string.Empty;
    public bool PublicClient { get; set; }
    public string ProtectedClientSecret { get; set; } = string.Empty;
    public string ProtectedAccessToken { get; set; } = string.Empty;
    public string ProtectedRefreshToken { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://127.0.0.1:17994/";
    public string ChannelId { get; set; } = string.Empty;
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    public void ClearTokens()
    {
        ProtectedAccessToken = string.Empty;
        ProtectedRefreshToken = string.Empty;
        AccessTokenExpiresAt = null;
    }

    public string GetClientSecret() => SecretProtector.Unprotect(ProtectedClientSecret);
    public string GetAccessToken() => SecretProtector.Unprotect(ProtectedAccessToken);
    public string GetRefreshToken() => SecretProtector.Unprotect(ProtectedRefreshToken);
    public void SetClientSecret(string value) => ProtectedClientSecret = SecretProtector.Protect(value);
    public void SetAccessToken(string value) => ProtectedAccessToken = SecretProtector.Protect(value);
    public void SetRefreshToken(string value) => ProtectedRefreshToken = SecretProtector.Protect(value);
}
