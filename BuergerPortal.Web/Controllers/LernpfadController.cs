using System.Text.Json;
using BuergerPortal.BundId;
using BuergerPortal.Web.Features.Lernpfad;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// Lernpfad „Schritt für Schritt“: Erklärseiten des Portals zu Schritt 1 (OIDC-Anfrage an den Anmeldedienst) und
    /// Schritt 5 (Code gegen Tokens, Sitzung). Schritt 2 und 4 zeigt der Auth-Server, Schritt 3 ist die BundID.
    /// Siehe Services/Lernpfad.cs.
    /// </summary>
    [Route("Lernpfad")]
    public class LernpfadController(IOptionsMonitor<OpenIdConnectOptions> oidc) : Controller
    {
        // Was das Portal nach der Anmeldung über die Person weiß (Auswahl, in dieser Reihenfolge)
        private static readonly string[] ClaimsZumZeigen =
        [
            "name", BundIdClaimTypes.GivenName, BundIdClaimTypes.FamilyName, BundIdClaimTypes.Birthdate,
            BundIdClaimTypes.Email, BundIdClaimTypes.TrustLevel, BundIdClaimTypes.IdentificationMethod,
            BundIdClaimTypes.Bpk2, BundIdClaimTypes.PostkorbHandle,
        ];

        // Schritt 1: Die OIDC-Anfrage kommt hierher statt zum Auth-Server (Lernpfad.ZurErklaerung) – genau so, wie sie
        // gleich zum Anmeldedienst geht. Nur Anfragen dieses Portals im Lernpfad; das Ziel ist fest der
        // Authorize-Endpunkt aus der Konfiguration (kein Open Redirect).
        [AllowAnonymous]
        [HttpGet("Anfrage")]
        public async Task<IActionResult> Anfrage(CancellationToken ct)
        {
            var options = oidc.Get(OpenIdConnectDefaults.AuthenticationScheme);
            if (Request.Query["client_id"] != options.ClientId || Request.Query[Lernpfad.Parameter] != "1"
                || options.ConfigurationManager == null)
            {
                return LocalRedirect("/");
            }
            var configuration = await options.ConfigurationManager.GetConfigurationAsync(ct);
            Response.Headers.CacheControl = "no-store";
            return View(new LernpfadAnfrageVm
            {
                Endpunkt = configuration.AuthorizationEndpoint,
                WeiterUrl = configuration.AuthorizationEndpoint + Request.QueryString.Value,
                Parameter = Request.Query
                    .Select(p => new LernpfadParameter(p.Key, Lernpfad.Kuerzen(p.Key, p.Value.ToString())))
                    .ToList(),
            });
        }

        // Schritt 5: angemeldet – was beim Code-Tausch ankam (aus der Anmeldung gemerkt, ohne Tokens) und welche
        // Angaben das Portal jetzt hat. Danach weiter zum ursprünglichen Ziel (nur lokale Adressen).
        [Authorize]
        [HttpGet("Angekommen")]
        public async Task<IActionResult> Angekommen(string? ziel)
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            LernpfadTokenInfo? info = null;
            if (auth.Properties?.Items.TryGetValue(Lernpfad.InfoKey, out var json) == true && !string.IsNullOrEmpty(json))
            {
                info = JsonSerializer.Deserialize<LernpfadTokenInfo>(json);
            }
            Response.Headers.CacheControl = "no-store";
            return View(new LernpfadAngekommenVm
            {
                Info = info,
                Ziel = !string.IsNullOrEmpty(ziel) && Url.IsLocalUrl(ziel) ? ziel : "/",
                Claims = ClaimsZumZeigen
                    .SelectMany(typ => User.FindAll(typ).Take(1))
                    .Select(c => new LernpfadParameter(c.Type,
                        c.Type is BundIdClaimTypes.Bpk2 or BundIdClaimTypes.PostkorbHandle ? Kurz(c.Value) : c.Value))
                    .ToList(),
            });
        }

        // Kennungen wie in „Meine Daten“ nur gekürzt zeigen
        private static string Kurz(string value) => value.Length > 12 ? value[..4] + "…" + value[^4..] : value;
    }
}
