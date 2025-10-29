using BuergerPortal.Web.Features.Termine;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class TermineController : Controller
    {
        private readonly IHttpClientFactory _cf;

        public TermineController(IHttpClientFactory cf)
        {
            _cf = cf;
        }

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
            => View(new BuchenVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buchen(BuchenVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            // Serverseitiger 15-Minuten-Check (zusätzlich zur UI)
            bool aligned = vm.LocalTime.TotalMinutes % 15 == 0 && vm.DurationMinutes % 15 == 0;
            if (!aligned)
            {
                ModelState.AddModelError(nameof(vm.LocalTime), "Nur 15-Minuten-Takt erlaubt.");
                return View(vm);
            }

            // Lokale Zeit (Europe/Berlin) -> UTC
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var localStart = vm.LocalDate.Date + vm.LocalTime; // Unspecified
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
            var endUtc = startUtc.AddMinutes(vm.DurationMinutes);

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new
            {
                service = vm.Service,        // Enum-Name/Wert muss zur API passen
                location = vm.Location,
                startUtc,
                endUtc
            };

            var res = await client.PostAsJsonAsync("api/appointments", payload, ct);

            if (res.IsSuccessStatusCode)
            {
                var id = await res.Content.ReadFromJsonAsync<Guid>(cancellationToken: ct);
                TempData["BookingSuccess"] = $"Termin gebucht ({id}).";
                return RedirectToAction(nameof(Index));
            }

            // ProblemDetails anzeigen (409 bei Kollision, 400 bei Validation etc.)
            var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            ModelState.AddModelError(string.Empty, problem?.Title ?? "Buchung fehlgeschlagen.");
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                ModelState.AddModelError(string.Empty, problem!.Detail);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Stornieren(Guid id)
        {
            // TODO: später POST /api/appointments/{id}/cancel
            TempData["BookingSuccess"] = "Termin wurde storniert (Mock).";
            return RedirectToAction(nameof(Index));
        }
    }
}
