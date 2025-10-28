using BuergerPortal.Web.Features.Termine;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class TermineController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            // TODO: Später aus Application-Layer laden (aktueller Benutzer)
            var vm = new TermineIndexVm
            {
                Termine = new()
                {
                    new TerminListItemVm
                    {
                        Id = Guid.NewGuid(),
                        Dienst = "Ausweis beantragen",
                        Datum = DateTime.Today.AddDays(2),
                        Uhrzeit = "09:30",
                        Standort = "Bürgeramt Mitte",
                        Storniert = false
                    },
                    new TerminListItemVm
                    {
                        Id = Guid.NewGuid(),
                        Dienst = "Meldebescheinigung",
                        Datum = DateTime.Today.AddDays(5),
                        Uhrzeit = "11:00",
                        Standort = "Bürgeramt Süd",
                        Storniert = false
                    }
                }
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult Buchen()
        {
            // Nächster Schritt: eigene Buchungsseite
            return View(); // (erstmal leere View – wir bauen sie im nächsten Schritt)
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Stornieren(Guid id)
        {
            // TODO: Application-Layer Command aufrufen (CancelAppointmentCommand)
            TempData["BookingSuccess"] = "Termin wurde storniert (Mock).";
            return RedirectToAction(nameof(Index));
        }
    }
}
