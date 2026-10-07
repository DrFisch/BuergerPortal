using BuergerPortal.BundId;
using AuthenticationServer.Data;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthenticationServer.Controllers
{
    public class AuthorizationController(UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
    {
        // Scope, mit dem ein Client die BundID-Daten der Person anfordert.
        public const string BundIdScope = "bundid";

        // Passt zu options.SetAuthorizationEndpointUris("/connect/authorize")
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            // Die aktuelle OIDC-Anfrage (client_id, scope, redirect_uri, ...)
            var oidcRequest = HttpContext.GetOpenIddictServerRequest()
                               ?? throw new InvalidOperationException("OIDC request not found.");

            // Anmeldung bei der BundID abgebrochen → dem Portal den OIDC-Fehler access_denied melden.
            if (TempData[BundIdController.CancelledKey] is string cancelled)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = cancelled,
                    }));
            }

            // Gefordertes Vertrauensniveau (OIDC-Parameter acr_values, z. B. "STORK-QAA-Level-3"); 0 = keine Vorgabe
            var requestedLevel = (oidcRequest.AcrValues ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(TrustLevel.Parse).OfType<int>().DefaultIfEmpty(0).Max();

            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var sessionLevel = authResult.Succeeded
                ? TrustLevel.Parse(authResult.Principal.FindFirstValue(BundIdClaimTypes.TrustLevel)) ?? 0
                : 0;

            // prompt=login (OIDC Core 3.1.2.1): Der Client verlangt eine neue Anmeldung, auch wenn hier noch eine
            // Sitzung besteht – das Portal fordert das bei jeder Anmeldung, damit immer die BundID gefragt wird
            // (dort ForceAuthn). Zurück geht es ohne prompt, sonst würde die Anfrage endlos zur Anmeldung führen.
            if (oidcRequest.HasPromptValue(PromptValues.Login))
            {
                var authorizeUrl = Request.PathBase + Request.Path + QueryString.Create(Request.Query
                    .Where(p => p.Key != Parameters.Prompt)
                    .Select(p => KeyValuePair.Create(p.Key, (string?)p.Value.ToString())));
                var level = requestedLevel > 0 ? $"level={requestedLevel}&" : string.Empty;
                return Redirect($"/bundid/login?{level}returnUrl={Uri.EscapeDataString(authorizeUrl)}");
            }

            // Step-up: Niveau gefordert, das die Sitzung nicht hat (oder keine Sitzung) → erneuter BundID-Login
            // mit diesem Niveau; danach geht es mit derselben Authorize-Anfrage weiter.
            if (requestedLevel > sessionLevel)
            {
                var authorizeUrl = Request.PathBase + Request.Path +
                    QueryString.Create(Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString()));
                return Redirect($"/bundid/login?level={requestedLevel}&returnUrl={Uri.EscapeDataString(authorizeUrl)}");
            }

            // 1) Benutzer nicht angemeldet? -> zur Anmeldung (LoginPath = BundID-Login)
            if (!authResult.Succeeded)
            {
                // nach Login soll’s automatisch hierher zurückkehren
                return Challenge(
                    authenticationSchemes: IdentityConstants.ApplicationScheme,
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.Path + QueryString.Create(Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString()))
                    });
            }

            // 2) Benutzer ist angemeldet -> Principal für Tokens bauen
            var user = await userManager.GetUserAsync(authResult.Principal)
                       ?? throw new InvalidOperationException("User not found.");

            // BundID-Daten stehen in der Sitzung (Identity-Cookie), nicht in der Datenbank.
            var session = authResult.Principal;
            var bundIdName = string.Join(' ', new[]
            {
                session.FindFirstValue(BundIdClaimTypes.GivenName),
                session.FindFirstValue(BundIdClaimTypes.FamilyName),
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var claims = new List<Claim>
        {
            new Claim(Claims.Subject, await userManager.GetUserIdAsync(user)),
            // Anzeigename: nach BundID-Login Vor- und Nachname, sonst der Benutzername
            new Claim(Claims.Name, bundIdName.Length > 0 ? bundIdName : await userManager.GetUserNameAsync(user) ?? string.Empty)
        };

            // E-Mail: aus der BundID-Sitzung, sonst aus dem Konto (alte lokale Konten)
            var email = session.FindFirstValue(BundIdClaimTypes.Email) ?? await userManager.GetEmailAsync(user);
            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(Claims.Email, email));

            // Weitere BundID-Daten nur, wenn der Client sie mit dem Scope "bundid" anfordert (Datensparsamkeit).
            if (oidcRequest.HasScope(BundIdScope))
            {
                claims.AddRange(session.Claims
                    .Where(c => BundIdClaimTypes.All.Contains(c.Type) && c.Type != BundIdClaimTypes.Email)
                    .Select(c => new Claim(c.Type, c.Value,
                        c.Type == BundIdClaimTypes.Address ? JsonClaimValueTypes.Json : ClaimValueTypes.String)));
                // EF liest datetime2 ohne Zeitzone; der Wert ist UTC und wird als solcher gekennzeichnet.
                if (user.LastLoginUtc is { } lastLogin)
                    claims.Add(new Claim(BundIdClaimTypes.LastLogin,
                        DateTime.SpecifyKind(lastLogin, DateTimeKind.Utc).ToString("O")));
            }

            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.AddClaims(claims);

            var principal = new ClaimsPrincipal(identity);

            // Scopes aus der Anfrage übernehmen (nur erlaubte)
            principal.SetScopes(oidcRequest.GetScopes());
            // Ressourcen (APIs) – damit Access Token ein "aud" hat
            principal.SetResources("buergerportal_api");

            // (Optional) Claim-Zielsetzung: was in id_token / access_token landen darf
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(claim.Type switch
                {
                    Claims.Name => new[] { Destinations.AccessToken, Destinations.IdentityToken },
                    Claims.Email => new[] { Destinations.AccessToken, Destinations.IdentityToken },
                    // Die API braucht Niveau (Step-up) und Postkorb-Handle (Nachrichten).
                    BundIdClaimTypes.TrustLevel or BundIdClaimTypes.PostkorbHandle
                        => new[] { Destinations.AccessToken, Destinations.IdentityToken },
                    // Persönliche Angaben nur ins ID-Token (Anzeige im Portal), nicht in jeden API-Aufruf.
                    _ when BundIdClaimTypes.All.Contains(claim.Type) || claim.Type == BundIdClaimTypes.LastLogin
                        => new[] { Destinations.IdentityToken },
                    _ => new[] { Destinations.AccessToken }
                });
            }

            // 3) Erfolg -> OpenIddict erzeugt Code/Token
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Identity-Cookie abmelden (Benutzer am Auth-Server ausloggen)
            await signInManager.SignOutAsync();

            // OIDC-Request lesen (enthält ggf. post_logout_redirect_uri)
            var request = HttpContext.GetOpenIddictServerRequest();

            // An OpenIddict „zurücksignen“, es erzeugt die OIDC-Logout-Antwort
            return SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = request?.PostLogoutRedirectUri ?? "/" // Fallback
                },
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
    }
}
