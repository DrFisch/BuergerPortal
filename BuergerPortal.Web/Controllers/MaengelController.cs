using BuergerPortal.Web.Features.Maengel.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class MaengelController : Controller
    {
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateMangelVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateMangelVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // TODO: hier später an Application Layer / Service übergeben
            // z.B. _mangelService.CreateAsync(...)

            // zum Testen einfach mal in TempData packen:
            TempData["Meldung"] = $"Mangel '{vm.Titel}' mit Koordinaten: {vm.Latitude}, {vm.Longitude}";
            return RedirectToAction("Create"); // oder auf eine Erfolgsseite
        }
    }
}
