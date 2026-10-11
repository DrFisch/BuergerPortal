using BuergerPortal.BundId;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// "Meine Daten": was die BundID bei der Anmeldung übermittelt hat (aus der Sitzung, nicht aus einer Datenbank),
    /// dazu Step-up und der Weg zum SAML-Inspektor des Auth-Servers.
    /// </summary>
    [Authorize]
    public class MeineDatenController(HttpCurrentUserService currentUser, IConfiguration configuration) : Controller
    {
        public IActionResult Index()
        {
            // Adresse des BundID-Postfachs (Postkorb-Simulation); ohne Konfiguration kein Link.
            ViewData["PostfachUrl"] = configuration["Postkorb:PostfachUrl"];
            // SAML-Inspektor im Auth-Server (standardmäßig an); abgeschaltet kein Link.
            ViewData["InspektorUrl"] = InspektorUrl();
            return View(currentUser.GetBundIdUser());
        }

        // SAML-Inspektor mit frischer Anmeldung: neue BundID-Anmeldung (prompt=login → ForceAuthn), danach in den
        // Inspektor. Ohne Anmeldung erlaubt, damit der Link von der Inspektor-Seite nicht zweimal anmelden lässt.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult SamlInspektorNeu()
        {
            if (InspektorUrl() == null)
            {
                return NotFound();
            }
            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(SamlInspektor)) };
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        // Weiter zum Inspektor des Auth-Servers – feste Adresse aus der Konfiguration, kein Ziel aus der Anfrage.
        [HttpGet]
        public IActionResult SamlInspektor() => InspektorUrl() is { } url ? Redirect(url) : NotFound();

        private string? InspektorUrl()
        {
            var authority = configuration["Authentication:Authority"];
            return configuration.GetValue("BundId:Inspektor:Enabled", true) && !string.IsNullOrWhiteSpace(authority)
                ? authority.TrimEnd('/') + "/bundid/inspektor"
                : null;
        }

        // Step-up zum Ausprobieren: erneute BundID-Anmeldung mit mindestens dem gewünschten Niveau (3 oder 4),
        // danach zurück zu "Meine Daten". Der Auth-Server gibt acr_values als Mindestniveau an die BundID weiter.
        [HttpGet]
        public IActionResult NiveauErhoehen(int level)
        {
            if (level is not (TrustLevel.Substantial or TrustLevel.High))
            {
                return RedirectToAction(nameof(Index));
            }
            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(Index)) };
            properties.Items["acr_values"] = TrustLevel.ToStork(level);
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
