using BuergerPortal.Web.Extensions;
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
                ViewBag.AuthNotice = "Bitte melde dich an, um deine Termine zu sehen und zu buchen.";
                apiItems = new();
            }
            else
            {
                res.EnsureSuccessStatusCode(); 
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
                        Service = x.Service,
                        Datum = startLocal.Date,
                        Uhrzeit = $"{startLocal:HH\\:mm} - {endLocal:HH\\:mm}",
                        Location = x.Location,
                        Storniert = x.Cancelled,
                        AntragId = x.AntragId
                    };
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Buchen(Guid? antragId, ServiceType? service)
        {
            var vm = new BuchenVm
            {
                // Wenn vom Link gekommen, Dienst vorbesetzen
                Service = service ,
                RelatedAntragId = antragId
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Buchen(BuchenVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            // 15 Minuten Check
            bool aligned = vm.LocalTime.TotalMinutes % 15 == 0 && vm.DurationMinutes % 15 == 0;
            if (!aligned)
            {
                ModelState.AddModelError(nameof(vm.LocalTime), "Nur 15-Minuten-Takt erlaubt.");
                return View(vm);
            }

            // Lokal (Europe/Berlin) -> UTC
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var localStart = vm.LocalDate.Date + vm.LocalTime; 
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
            var endUtc = startUtc.AddMinutes(vm.DurationMinutes);

            var client = _cf.CreateClient("BuergerPortalApi");

            
            var payload = new
            {
                service = vm.Service,
                location = vm.Location,
                startUtc,
                endUtc,
                antragId = vm.RelatedAntragId
            };

            var res = await client.PostAsJsonAsync("api/appointments", payload, ct);

            if (res.IsSuccessStatusCode)
            {
                var id = await res.Content.ReadFromJsonAsync<Guid>(cancellationToken: ct);

                // Allgemeine Success-TempData 
                TempData["BookingSuccess"] = $"Termin gebucht ({id}).";

                if (vm.RelatedAntragId.HasValue)
                {
                    TempData["AntragTerminOk"] = "Termin zum Antrag gebucht.";
                    return RedirectToAction("Antrag", "Antraege", new { id = vm.RelatedAntragId.Value });
                }

                // sonst zur Termin-Übersicht 
                return RedirectToAction(nameof(Index));
            }

            // Fehlerbehandlung 
            ProblemDetails? problem = null;
            try
            {
                var ctHeader = res.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrEmpty(ctHeader) &&
                    (ctHeader.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                     ctHeader.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase)))
                {
                    problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
                }
            }
            catch (System.Text.Json.JsonException) {  }

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
        [Authorize] 
        public async Task<IActionResult> Busy([FromQuery] string date, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(date))
                return BadRequest("date (YYYY-MM-DD) fehlt.");

            var client = _cf.CreateClient("BuergerPortalApi"); 
            var res = await client.GetAsync($"api/appointments/busy?date={date}", ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Unauthorized(); 

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var items = await res.Content.ReadFromJsonAsync<List<BusySlotResponse>>(cancellationToken: ct)
                        ?? new List<BusySlotResponse>();

            return Json(items);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] 
        public async Task<IActionResult> Stornieren(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty)
            {
                TempData["BookingError"] = "Ungültige Termin-ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var client = _cf.CreateClient("BuergerPortalApi"); 
                                                                   
                var res = await client.PostAsync($"api/appointments/{id}/cancel", content: null, ct);

                
                if (res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    TempData["BookingError"] = "Nicht autorisiert. Bitte erneut anmelden.";
                    return RedirectToAction(nameof(Index));
                }

                if (!res.IsSuccessStatusCode)
                {
                    var msg = await res.Content.ReadAsStringAsync(ct);
                    TempData["BookingError"] = string.IsNullOrWhiteSpace(msg) ? "Stornierung fehlgeschlagen." : msg;
                    return RedirectToAction(nameof(Index));
                }

                TempData["BookingSuccess"] = "Termin wurde storniert.";
            }
            catch (Exception)
            {
                TempData["BookingError"] = "Stornierung derzeit nicht möglich.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(Guid id, CancellationToken ct)
        {
            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/appointments/{id}", ct);

            if (!res.IsSuccessStatusCode)
            {
                TempData["BookingError"] = "Termin konnte nicht geladen werden.";
                return RedirectToAction(nameof(Index));
            }

            var x = await res.Content.ReadFromJsonAsync<AppointmentListItemResponse>(cancellationToken: ct);
            if (x == null) return NotFound();

            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(x.StartUtc, BerlinTz);

            var vm = new TerminDetailsVm
            {
                Id = x.Id,
                Service = x.Service, 
                Datum = startLocal.Date,
                Uhrzeit = $"{startLocal:HH\\:mm}",
                Location = x.Location,
                Storniert = x.Cancelled,
                AntragId = x.AntragId
            };

            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> UpdateLocation(TerminDetailsVm vm, CancellationToken ct)
        {
            if (vm.Id == Guid.Empty)
            {
                TempData["BookingError"] = "Ungültige Termin-ID.";
                return RedirectToAction(nameof(Index));
            }

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new { newLocation = vm.Location };

            var res = await client.PatchAsJsonAsync($"api/appointments/{vm.Id}/location", payload, ct);

            if (res.IsSuccessStatusCode)
            {
                TempData["BookingSuccess"] = "Standort wurde erfolgreich geändert.";
                return RedirectToAction(nameof(Index));
            }

            var errorMsg = await res.Content.ReadAsStringAsync(ct);
            ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(errorMsg)
                ? "Der Standort konnte nicht geändert werden."
                : errorMsg);

            return View("Details", vm);
        }
    }
}
