using BuergerPortal.Web.Extensions;
using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    [Authorize]
    public class SperrmuellAntraegeController : Controller
    {
        private readonly IHttpClientFactory _cf;
        private readonly HttpCurrentUserService _currentUser;

        // Meldungen: deutscher Text ist Schlüssel und Rückfall, Englisch in Resources/Controllers/SperrmuellAntraegeController.en.resx (Plan A9)
        private readonly IStringLocalizer<SperrmuellAntraegeController> _t;

        public SperrmuellAntraegeController(IHttpClientFactory cf, HttpCurrentUserService currentUser,
            IStringLocalizer<SperrmuellAntraegeController> t)
        {
            _cf = cf;
            _currentUser = currentUser;
            _t = t;
        }

        [HttpGet]
        public IActionResult NeuerSperrmuell()
        {
            var vm = new SperrmuellStep1Vm();
            // Vorbefüllen aus der BundID-Anmeldung; alle Felder bleiben änderbar.
            if (_currentUser.GetBundIdUser() is { IsBundIdLogin: true } user)
            {
                vm.Vorname = user.GivenName ?? string.Empty;
                vm.Nachname = user.FamilyName ?? string.Empty;
                vm.Geburtsdatum = user.Birthdate?.ToDateTime(TimeOnly.MinValue);
                vm.Email = user.Email;
                ViewData["AusBundId"] = true;
            }
            return View("SperrmuellStep1", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SperrmuellStep1(SperrmuellStep1Vm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return View("SperrmuellStep1", vm);

            if (vm.Geburtsdatum is null)
            {
                ModelState.AddModelError(nameof(vm.Geburtsdatum), _t["Bitte ein gültiges Datum wählen."]);
                return View("SperrmuellStep1", vm);
            }

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new SperrmuellStep1Request
            {
                Vorname = vm.Vorname.Trim(),
                Nachname = vm.Nachname.Trim(),
                Geburtsdatum = DateOnly.FromDateTime(vm.Geburtsdatum.Value.Date),
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                Telefon = string.IsNullOrWhiteSpace(vm.Telefon) ? null : vm.Telefon.Trim()
            };

            var res = await client.PostAsJsonAsync("api/antraege/sperrmuell/step1", payload, ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!res.IsSuccessStatusCode)
                return View("SperrmuellStep1", await AddModelErrorsAndReturn(vm, res, ct));

            var idObj = await res.Content.ReadFromJsonAsync<ApiIdResponse>(cancellationToken: ct);
            if (idObj is null || idObj.Id == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty, _t["Unerwartete Antwort der API."]);
                return View("SperrmuellStep1", vm);
            }

            return RedirectToAction(nameof(SperrmuellStep2), new { id = idObj.Id });
        }

        [HttpGet]
        public async Task<IActionResult> SperrmuellStep2(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty)
                return BadRequest();

            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/antraege/sperrmuell/{id}", ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var detail = await res.Content.ReadFromJsonAsync<SperrmuellDetailResponse>(cancellationToken: ct);
            if (detail is null)
                return NotFound();

            DateTime defaultDate = DateTime.Today.AddDays(1);
            
            // Wenn das Standard-Datum ein Wochenende ist, auf nächsten Werktag verschieben
            while (defaultDate.DayOfWeek == DayOfWeek.Saturday || defaultDate.DayOfWeek == DayOfWeek.Sunday)
            {
                defaultDate = defaultDate.AddDays(1);
            }

            var vm = new SperrmuellStep2Vm
            {
                Id = detail.Id,
                AntragstellerName = $"{detail.Vorname} {detail.Nachname}",
                Geburtsdatum = detail.Geburtsdatum.ToDateTime(TimeOnly.MinValue),

                Strasse = detail.Strasse,
                PLZ = detail.PLZ,
                Ort = detail.Ort,

                HolzKubikmeter = detail.HolzKubikmeter,
                SonstigesKubikmeter = detail.SonstigesKubikmeter,
                Matratzen = detail.Matratzen,

                Wunschzeit = detail.Wunschzeit == default
                    ? defaultDate
                    : detail.Wunschzeit.ToLocalTime(),
                Hinweis = detail.Hinweis
            };

            // Noch keine Abholadresse eingetragen: Anschrift aus der BundID vorschlagen (änderbar).
            if (string.IsNullOrWhiteSpace(vm.Strasse)
                && _currentUser.GetBundIdUser()?.Address is { StreetAddress: not null } address)
            {
                vm.Strasse = address.StreetAddress;
                vm.PLZ = address.PostalCode ?? string.Empty;
                vm.Ort = address.Locality ?? string.Empty;
                ViewData["AusBundId"] = true;
            }

            return View("SperrmuellStep2", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SperrmuellStep2(Guid id, SperrmuellStep2Vm vm, string? submitAction, CancellationToken ct)
        {
            if (id == Guid.Empty || vm.Id == Guid.Empty || id != vm.Id)
                return BadRequest();

            if (vm.Wunschzeit is null)
            {
                ModelState.AddModelError(nameof(vm.Wunschzeit), _t["Bitte ein gültiges Datum wählen."]);
                return View("SperrmuellStep2", vm);
            }

            // Wochenend-Validierung
            var dayOfWeek = vm.Wunschzeit.Value.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                ModelState.AddModelError(nameof(vm.Wunschzeit), _t["Am Wochenende ist keine Abholung möglich. Bitte wählen Sie einen Werktag."]);
                return View("SperrmuellStep2", vm);
            }

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new SperrmuellStep2Request
            {
                Strasse = vm.Strasse.Trim(),
                PLZ = vm.PLZ.Trim(),
                Ort = vm.Ort.Trim(),
                HolzKubikmeter = vm.HolzKubikmeter,
                SonstigesKubikmeter = vm.SonstigesKubikmeter,
                Matratzen = vm.Matratzen,
                Wunschzeit = vm.Wunschzeit.Value,
                Hinweis = string.IsNullOrWhiteSpace(vm.Hinweis) ? null : vm.Hinweis.Trim()
            };

            var put = await client.PutAsJsonAsync($"api/antraege/sperrmuell/{id}/step2", payload, ct);

            if (put.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!put.IsSuccessStatusCode)
                return View("SperrmuellStep2", await AddModelErrorsAndReturn(vm, put, ct));

            // Seit 2026-10-18 führt der Assistent über „Prüfen und absenden“; "submit" (direkt einreichen) bleibt für
            // Skripte (sperrmuell-einreichen.sh) erhalten.
            if (submitAction == "entwurf")
            {
                TempData["AntragSuccess"] = _t["Entwurf gespeichert – Sie finden ihn unter „Anträge“."].Value;
                return RedirectToAction("Index", "Antraege");
            }
            if (submitAction != "submit")
            {
                return RedirectToAction(nameof(SperrmuellPruefen), new { id });
            }

            var submit = await client.PostAsync($"api/antraege/sperrmuell/{id}/submit", content: null, ct);

            if (submit.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!submit.IsSuccessStatusCode)
                return View("SperrmuellStep2", await AddModelErrorsAndReturn(vm, submit, ct));

            TempData["AntragSuccess"] = _t["Sperrmüllantrag eingereicht."].Value;
            TempData.MerkePostkorbStatus(submit);
            return RedirectToAction("Status", "Antraege");
        }

        // Schritt 3 „Prüfen und absenden“ (2026-10-18)
        [HttpGet]
        public async Task<IActionResult> SperrmuellPruefen(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            var res = await _cf.CreateClient("BuergerPortalApi").GetAsync($"api/antraege/sperrmuell/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var d = await res.Content.ReadFromJsonAsync<SperrmuellDetailResponse>(cancellationToken: ct);
            if (d is null) return NotFound();
            if ((int)d.Status != 1) return RedirectToAction("SperrmuellAntrag", "Antraege", new { id });

            return View("SperrmuellPruefen", new SperrmuellPruefenVm
            {
                Id = d.Id,
                Antragsteller = $"{d.Vorname} {d.Nachname}",
                Geburtsdatum = d.Geburtsdatum.ToDateTime(TimeOnly.MinValue),
                Email = d.Email,
                Telefon = d.Telefon,
                Strasse = d.Strasse,
                PLZ = d.PLZ,
                Ort = d.Ort,
                HolzKubikmeter = d.HolzKubikmeter,
                SonstigesKubikmeter = d.SonstigesKubikmeter,
                Matratzen = d.Matratzen,
                Wunschzeit = d.Wunschzeit.ToLocalTime(),
                Hinweis = d.Hinweis,
            });
        }

        // Verbindlich absenden (Niveau „normal“ reicht – kein Step-up), danach zur Detailseite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SperrmuellEinreichen(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            var submit = await _cf.CreateClient("BuergerPortalApi").PostAsync($"api/antraege/sperrmuell/{id}/submit", content: null, ct);
            if (submit.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!submit.IsSuccessStatusCode)
            {
                TempData["BookingError"] = _t["Der Antrag ließ sich nicht absenden. Bitte versuchen Sie es noch einmal."].Value;
                return RedirectToAction(nameof(SperrmuellPruefen), new { id });
            }

            TempData["AntragSuccess"] = _t["Sperrmüllantrag eingereicht."].Value;
            TempData.MerkePostkorbStatus(submit);
            return RedirectToAction("SperrmuellAntrag", "Antraege", new { id });
        }

        // -------- Helpers ----------
        private async Task<TVm> AddModelErrorsAndReturn<TVm>(TVm vm, HttpResponseMessage res, CancellationToken ct)
        {
            try
            {
                var ctHeader = res.Content.Headers.ContentType?.MediaType ?? "";
                if (ctHeader.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    var p = await res.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
                    if (p is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Title))
                            ModelState.AddModelError(string.Empty, p.Title);
                        if (!string.IsNullOrWhiteSpace(p.Detail))
                            ModelState.AddModelError(string.Empty, p.Detail);
                        return vm;
                    }
                }
            }
            catch { }

            var raw = await res.Content.ReadAsStringAsync(ct);
            ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(raw)
                ? $"Fehler {(int)res.StatusCode} {res.ReasonPhrase}"
                : raw);
            return vm;
        }
    }
}
