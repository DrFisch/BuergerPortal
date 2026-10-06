using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        [HttpGet]
        public IActionResult LoginRequired(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;
            return View();
        }

        // Verständliche Seite, wenn die Anmeldung nicht geklappt hat
        // (grund: abgebrochen = Abbruch bei der BundID, nicht-erreichbar, sonst allgemeiner Fehler).
        [HttpGet]
        public IActionResult Fehler(string? grund = null)
        {
            ViewData["Grund"] = grund;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string? returnUrl = null)
        {
            // Schutz vor Open Redirect: Nach dem Login nur zu Adressen dieses Portals zurück.
            var redirectUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");

            return Challenge(
                new AuthenticationProperties { RedirectUri = redirectUrl },
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
