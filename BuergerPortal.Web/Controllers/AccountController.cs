using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = "/")
        {
            // Schutz vor Open Redirect: Nach dem Login nur zu Adressen dieses Portals zurück.
            var redirectUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            return Challenge(
                    new AuthenticationProperties { RedirectUri = redirectUrl },
                    OpenIdConnectDefaults.AuthenticationScheme);
        }

        // Ohne Sitzung (z. B. abgelaufen) nicht erst zur Anmeldung schicken – sonst würde man nach dem Login sofort
        // wieder abgemeldet. Einfach zur Startseite.
        [AllowAnonymous]
        public IActionResult Logout()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return LocalRedirect("/");
            }
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
