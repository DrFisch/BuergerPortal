using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Erneuert das Access-Token des Portals mit dem Refresh-Token (OAuth 2.0 grant_type=refresh_token) am Token-Endpunkt
/// des Auth-Servers. Die neuen Tokens landen in den AuthenticationProperties des Sitzungscookies.
/// </summary>
public sealed class AccessTokenRefresher(IOptionsMonitor<OpenIdConnectOptions> oidcOptions, ILogger<AccessTokenRefresher> logger)
{
    /// <summary>Gespeicherter Ablaufzeitpunkt des Access-Tokens ("expires_at", von SaveTokens geschrieben).</summary>
    public static DateTimeOffset? ExpiresAt(AuthenticationProperties properties) =>
        DateTimeOffset.TryParse(properties.GetTokenValue("expires_at"), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var expiresAt) ? expiresAt : null;

    public async Task<bool> TryRefreshAsync(AuthenticationProperties properties, CancellationToken ct)
    {
        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        try
        {
            var configuration = await options.ConfigurationManager!.GetConfigurationAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = options.ClientId ?? string.Empty,
                    ["client_secret"] = options.ClientSecret ?? string.Empty,
                })
            };
            using var response = await options.Backchannel.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Refresh-Token abgelehnt ({Status}).", (int)response.StatusCode);
                return false;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;
            var expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
            properties.UpdateTokenValue("access_token", root.GetProperty("access_token").GetString()!);
            // Rotierende Refresh-Tokens: das neue ersetzt das verbrauchte.
            if (root.TryGetProperty("refresh_token", out var newRefresh) && newRefresh.GetString() is { Length: > 0 } rt)
            {
                properties.UpdateTokenValue("refresh_token", rt);
            }
            properties.UpdateTokenValue("expires_at",
                DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));
            logger.LogInformation("Access-Token mit dem Refresh-Token erneuert.");
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or InvalidOperationException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "Erneuern des Access-Tokens fehlgeschlagen.");
            return false;
        }
    }
}
