using BuergerPortal.BundId;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// "Meine Daten": was die BundID bei der Anmeldung übermittelt hat (aus der Sitzung, nicht aus einer Datenbank).
    /// </summary>
    [Authorize]
    public class MeineDatenController(HttpCurrentUserService currentUser, IConfiguration configuration) : Controller
    {
        public IActionResult Index()
        {
            // Adresse des BundID-Postfachs (Postkorb-Simulation); ohne Konfiguration kein Link.
            ViewData["PostfachUrl"] = configuration["Postkorb:PostfachUrl"];
            return View(currentUser.GetBundIdUser());
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
