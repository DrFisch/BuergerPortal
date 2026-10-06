using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// "Meine Daten": was die BundID bei der Anmeldung übermittelt hat (aus der Sitzung, nicht aus einer Datenbank).
    /// </summary>
    [Authorize]
    public class MeineDatenController(HttpCurrentUserService currentUser) : Controller
    {
        public IActionResult Index() => View(currentUser.GetBundIdUser());
    }
}
