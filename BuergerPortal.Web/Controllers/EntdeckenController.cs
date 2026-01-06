using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class EntdeckenController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
