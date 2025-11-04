using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class AntraegeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Status() { return View(); }

        [HttpGet]
        public IActionResult NeuerReisepass() { return View(); }

        [HttpGet]
        public IActionResult NeuerSperrmuell() { return View(); }
    }
}
