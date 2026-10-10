using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// Sprachwahl in der Kopfzeile (Views/Shared/_Sprachwahl.cshtml): setzt das Sprach-Cookie und führt zurück zur Seite.
    /// Ohne Anmeldung erreichbar – die Sprache soll man schon auf der Einstiegsseite wählen können. Standard ist Deutsch.
    /// </summary>
    [AllowAnonymous]
    public sealed class LanguageController : Controller
    {
        public static readonly string[] Supported = ["de", "en"];

        [HttpGet("/sprache/{code}")]
        public IActionResult Set(string code, string? returnUrl)
        {
            if (!Supported.Contains(code))
            {
                return NotFound();
            }

            Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(code)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });

            // nur zurück auf eine Seite des Portals (kein Open Redirect)
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }
    }
}
