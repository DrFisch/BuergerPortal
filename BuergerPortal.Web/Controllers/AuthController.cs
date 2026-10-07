using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        // Frühere Seite "Anmeldung erforderlich": Es gibt nur noch die Einstiegsseite (mit Rücksprungziel).
        [HttpGet]
        public IActionResult LoginRequired(string? returnUrl = null)
        {
            var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            return RedirectToAction("Index", "Home", new { returnUrl = target });
        }

        // Verständliche Seite, wenn die Anmeldung nicht geklappt hat
        // (grund: abgebrochen = Abbruch bei der BundID, nicht-erreichbar, sonst allgemeiner Fehler).
        [HttpGet]
        public IActionResult Fehler(string? grund = null)
        {
            ViewData["Grund"] = grund;
            return View();
        }

        // Restzeit der Sitzung für die Warnung vor dem automatischen Abmelden (wwwroot/js/sitzung.js).
        // GET fragt nur ab (zählt nicht als Aktivität), POST = „Angemeldet bleiben“ (verlängert, siehe PortalSession).
        [HttpGet(PortalSession.StatusPath)]
        [HttpPost(PortalSession.StatusPath)]
        [IgnoreAntiforgeryToken] // verlängert nur die eigene Sitzung
        public async Task<IActionResult> Sitzung()
        {
            var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Properties is null)
            {
                return Json(new { angemeldet = false, restSekunden = 0, hoechstdauer = false });
            }

            var now = DateTimeOffset.UtcNow;
            var login = PortalSession.LoginTime(auth.Properties);
            var rest = HttpMethods.IsPost(Request.Method)
                ? PortalSession.Remaining(now, login)
                : Min(auth.Properties.ExpiresUtc - now ?? TimeSpan.Zero,
                      login is null ? PortalSession.MaxLifetime : login.Value + PortalSession.MaxLifetime - now);
            var hoechstdauer = login is not null && login.Value + PortalSession.MaxLifetime - now <= PortalSession.IdleTimeout;
            return Json(new { angemeldet = true, restSekunden = (int)Math.Max(0, rest.TotalSeconds), hoechstdauer });

            static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
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
