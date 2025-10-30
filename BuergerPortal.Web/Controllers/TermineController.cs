using BuergerPortal.Web.Features.Termine;
using BuergerPortal.Web.Features.Termine.Contracts;
using BuergerPortal.Web.Features.Termine.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    public class TermineController : Controller
    {
        private readonly IHttpClientFactory _cf;
        private static readonly TimeZoneInfo BerlinTz =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        public TermineController(IHttpClientFactory cf)
        {
            _cf = cf;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var client = _cf.CreateClient("BuergerPortalApi");

            List<AppointmentListItemResponse> apiItems;

            var res = await client.GetAsync("api/appointments/mine", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Hinweis für die View
                ViewBag.AuthNotice = "Bitte melde dich an, um deine Termine zu sehen und zu buchen.";
                apiItems = new();
            }
            else
            {
                res.EnsureSuccessStatusCode(); // andere Fehler sauber hochwerfen
                apiItems = await res.Content.ReadFromJsonAsync<List<AppointmentListItemResponse>>(cancellationToken: ct)
                           ?? new();
            }

            DateTime ToBerlin(DateTime utc) =>
                TimeZoneInfo.ConvertTimeFromUtc(
                    utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                    BerlinTz);

            var vm = new TermineIndexVm
            {
                Termine = apiItems.Select(x =>
                {
                    var startLocal = ToBerlin(x.StartUtc);
                    var endLocal = ToBerlin(x.EndUtc);
                    return new TerminListItemVm
                    {
                        Id = x.Id,
                        Dienst = x.Service.ToString(),
                        Datum = startLocal.Date,
                        Uhrzeit = $"{startLocal:HH\\:mm} - {endLocal:HH\\:mm}",
                        Standort = x.Location,
                        Storniert = x.Cancelled
                    };
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Buchen()
            => View(new BuchenVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
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

            // --- robust: ProblemDetails bevorzugt, sonst Plain-Text/Fallback
            ProblemDetails? problem = null;
            try
            {
                // Nur versuchen, wenn Content-Typ plausibel ist
                var ctHeader = res.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrEmpty(ctHeader) &&
                    (ctHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                     ctHeader.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase)))
                {
                    problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Ignorieren, wir fallen unten auf Raw-Text/Fallback zurück
            }

            // Falls kein ProblemDetails geparst werden konnte, baue eines
            if (problem is null)
            {
                var raw = await res.Content.ReadAsStringAsync(ct);
                problem = new ProblemDetails
                {
                    Title = $"Fehler {(int)res.StatusCode} {res.ReasonPhrase}",
                    Detail = string.IsNullOrWhiteSpace(raw) ? null : raw,
                    Status = (int)res.StatusCode
                };
            }

            ModelState.AddModelError(string.Empty, problem.Title ?? "Buchung fehlgeschlagen.");
            if (!string.IsNullOrWhiteSpace(problem.Detail))
                ModelState.AddModelError(string.Empty, problem.Detail);

            return View(vm);

        }
        [HttpGet]
        [Authorize] // falls nicht auf Controller gesetzt
        public async Task<IActionResult> Busy([FromQuery] string date, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(date))
                return BadRequest("date (YYYY-MM-DD) fehlt.");

            var client = _cf.CreateClient("BuergerPortalApi"); // <-- wichtiger named client mit AccessTokenHandler
            var res = await client.GetAsync($"api/appointments/busy?date={date}", ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized(); // an den Browser durchreichen

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var items = await res.Content.ReadFromJsonAsync<List<BusySlotResponse>>(cancellationToken: ct)
                        ?? new List<BusySlotResponse>();

            return Json(items);
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
