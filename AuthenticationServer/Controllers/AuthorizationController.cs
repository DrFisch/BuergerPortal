using AuthenticationServer.Data;
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
        // Passt zu options.SetAuthorizationEndpointUris("/connect/authorize")
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            // Die aktuelle OIDC-Anfrage (client_id, scope, redirect_uri, ...)
            var oidcRequest = HttpContext.GetOpenIddictServerRequest()
                               ?? throw new InvalidOperationException("OIDC request not found.");

            // 1) Benutzer nicht angemeldet? -> zur Identity-Loginseite
            var authResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
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

            var claims = new List<Claim>
        {
            new Claim(Claims.Subject, await userManager.GetUserIdAsync(user)),
            new Claim(Claims.Name, await userManager.GetUserNameAsync(user))
        };

            // Optional: Email-Claim, falls vorhanden
            var email = await userManager.GetEmailAsync(user);
            if (!string.IsNullOrEmpty(email))
                claims.Add(new Claim(Claims.Email, email));

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
