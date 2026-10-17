using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace BuergerPortal.Web.Services
{
    /// <summary>
    /// Lernpfad „Schritt für Schritt“ (Wunsch Lukas 2026-10-17): Wählt man im Hinweis vor der BundID-Simulation
    /// „Schritt für Schritt ansehen“, hält die Anmeldung an jeder Station an und zeigt, was technisch passiert –
    /// Schritt 1 (OIDC-Anfrage) und 5 (Tokens, Sitzung) im Portal, 2 (AuthnRequest) und 4 (geprüfte Response) im
    /// Auth-Server, Schritt 3 ist die Identifizierung bei der BundID selbst. Die Nachrichten sind die echten dieser
    /// Anmeldung; Tokens selbst zeigt keine Seite.
    /// </summary>
    public static class Lernpfad
    {
        /// <summary>Kurzlebiges Cookie, das der Hinweis-Dialog setzt (wwwroot/js/bundid-hinweis.js).</summary>
        public const string CookieName = "__Host-bp-lernpfad";

        /// <summary>Zusätzlicher Parameter der OIDC-Anfrage; der Auth-Server hält daran Schritt 2 und 4 an.</summary>
        public const string Parameter = "bpsim_lernpfad";

        /// <summary>Merker in den AuthenticationProperties der Anmeldung (bleibt über den Rückweg erhalten).</summary>
        public const string PropertyKey = "bpsim_lernpfad";

        /// <summary>Was das Portal beim Code-Tausch bekam (JSON, ohne Tokens) – für die Seite zu Schritt 5.</summary>
        public const string InfoKey = "bpsim_lernpfad_info";

        public const string AnfragePfad = "/Lernpfad/Anfrage";
        public const string AngekommenPfad = "/Lernpfad/Angekommen";

        public static bool IsRequested(HttpRequest request) =>
            request.Cookies.TryGetValue(CookieName, out var value) && value == "1";

        /// <summary>
        /// Schritt 1: Statt direkt zum Auth-Server geht der Browser zur Erklärseite des Portals – mit genau der
        /// OIDC-Anfrage (state, nonce, code_challenge …) als Query, dazu bpsim_lernpfad=1. Von dort führt ein Knopf
        /// zum echten Authorize-Endpunkt. Das Cookie wird dabei verbraucht.
        /// </summary>
        public static void ZurErklaerung(RedirectContext ctx)
        {
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Secure = true, Path = "/" });
            ctx.Properties.Items[PropertyKey] = "1";
            ctx.ProtocolMessage.SetParameter(Parameter, "1");
            var request = ctx.Request;
            ctx.ProtocolMessage.IssuerAddress = $"{request.Scheme}://{request.Host}{request.PathBase}{AnfragePfad}";
        }

        /// <summary>
        /// Schritt 5: Nach der Prüfung des ID-Tokens merken, was ankam (Algorithmus, Aussteller, Gültigkeit, welche
        /// Tokens), und danach zur Erklärseite statt zum ursprünglichen Ziel; das Ziel geht als Parameter mit.
        /// </summary>
        public static void TokensMerken(TokenValidatedContext ctx)
        {
            if (ctx.Properties == null || !ctx.Properties.Items.Remove(PropertyKey))
            {
                return;
            }
            var info = LernpfadTokenInfo.Create(ctx.SecurityToken, ctx.TokenEndpointResponse);
            ctx.Properties.Items[InfoKey] = JsonSerializer.Serialize(info);
            var ziel = string.IsNullOrEmpty(ctx.Properties.RedirectUri) ? "/" : ctx.Properties.RedirectUri;
            ctx.Properties.RedirectUri = $"{AngekommenPfad}?ziel={Uri.EscapeDataString(ziel)}";
        }

        /// <summary>Werte, die nicht ganz gezeigt werden (Zufallswerte und geschützte Daten): Anfang + „…“.</summary>
        public static string Kuerzen(string name, string value) =>
            name is OpenIdConnectParameterNames.State or OpenIdConnectParameterNames.Nonce or "code_challenge"
                && value.Length > 16
                ? value[..12] + "…"
                : value;
    }

    /// <summary>Was das Portal beim Code-Tausch erhalten hat – ohne die Tokens selbst.</summary>
    public sealed record LernpfadTokenInfo
    {
        public string? Algorithmus { get; init; }
        public string? Schluessel { get; init; }
        public string? Aussteller { get; init; }
        public string? Zielgruppe { get; init; }
        public DateTime? AusgestelltUtc { get; init; }
        public DateTime? GueltigBisUtc { get; init; }
        public string? Niveau { get; init; }
        public string? Mittel { get; init; }
        public bool NonceGeprueft { get; init; }
        public IReadOnlyList<string> Tokens { get; init; } = [];
        public int? AccessTokenSekunden { get; init; }
        public string? Scope { get; init; }

        public static LernpfadTokenInfo Create(JwtSecurityToken? idToken, OpenIdConnectMessage? tokenResponse)
        {
            var tokens = new List<string>();
            if (!string.IsNullOrEmpty(tokenResponse?.IdToken)) tokens.Add("id_token");
            if (!string.IsNullOrEmpty(tokenResponse?.AccessToken)) tokens.Add("access_token");
            if (!string.IsNullOrEmpty(tokenResponse?.RefreshToken)) tokens.Add("refresh_token");
            return new LernpfadTokenInfo
            {
                Algorithmus = idToken?.Header.Alg,
                Schluessel = idToken?.Header.Kid is { Length: > 12 } kid ? kid[..12] + "…" : idToken?.Header.Kid,
                Aussteller = idToken?.Issuer,
                Zielgruppe = idToken == null ? null : string.Join(", ", idToken.Audiences),
                AusgestelltUtc = idToken?.IssuedAt is { Year: > 1970 } iat ? iat : null,
                GueltigBisUtc = idToken?.ValidTo is { Year: > 1970 } exp ? exp : null,
                Niveau = idToken?.Claims.FirstOrDefault(c => c.Type == "acr")?.Value,
                Mittel = idToken?.Claims.FirstOrDefault(c => c.Type == "amr")?.Value,
                // Die Middleware hat das ID-Token erst nach erfolgreicher Prüfung (Signatur, iss, aud, exp, nonce) übergeben
                NonceGeprueft = idToken?.Claims.Any(c => c.Type == "nonce") == true,
                Tokens = tokens,
                AccessTokenSekunden = int.TryParse(tokenResponse?.ExpiresIn, out var s) ? s : null,
                Scope = tokenResponse?.Scope,
            };
        }
    }
}
