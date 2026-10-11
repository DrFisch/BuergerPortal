using AuthenticationServer.BundId;
using AuthenticationServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AuthenticationServer.Controllers
{
    /// <summary>
    /// SAML-Inspektor („Blick unter die Haube“): zeigt nach der Anmeldung die gerade empfangene Assertion, jede Prüfung
    /// mit Soll und Ist und die Abbildung Attribut → Claim. Nur die eigene Anmeldung (dieser Browser, dieses Konto), nur
    /// wenige Minuten, nie Tokens. Standardmäßig an; mit BundId:Inspektor:Enabled=false antwortet die Adresse 404.
    /// </summary>
    [AllowAnonymous]   // ohne Anmeldung ein Hinweis statt einer Umleitung zur BundID
    [Route("bundid/inspektor")]
    public class BundIdInspektorController(BundIdInspectionStore store, IOptions<BundIdInspektorOptions> options) : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            var settings = options.Value;
            if (!settings.Enabled)
            {
                return NotFound();
            }

            // Konto aus der Identity-Sitzung des Auth-Servers (entsteht bei der BundID-Anmeldung am ACS).
            var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            // Persönliche Daten: nicht zwischenspeichern, nicht in Suchmaschinen.
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Robots-Tag"] = "noindex";
            return View(new BundIdInspektorViewModel
            {
                Entry = store.Find(Request, userId),
                MinutesVisible = settings.MinutesVisible,
                PortalUrl = string.IsNullOrWhiteSpace(settings.PortalUrl) ? null : settings.PortalUrl.TrimEnd('/'),
            });
        }
    }
}
