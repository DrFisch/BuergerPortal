using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    [Authorize] 
    public class ReisepassAntraegeController : Controller
    {
        private readonly IHttpClientFactory _cf;
        public ReisepassAntraegeController(IHttpClientFactory cf) => _cf = cf;

        [HttpGet]
        public IActionResult NeuerReisepass()
        {
            return View("ReisepassStep1", new ReisepassStep1Vm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReisepassStep1(ReisepassStep1Vm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View("ReisepassStep1", vm);

            if (vm.Geburtsdatum is null)
            {
                ModelState.AddModelError(nameof(vm.Geburtsdatum), "Bitte ein gültiges Datum wählen.");
                return View("ReisepassStep1", vm);
            }

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new ReisepassStep1Request
            {
                Vorname = vm.Vorname.Trim(),
                Nachname = vm.Nachname.Trim(),
                Geburtsdatum = DateOnly.FromDateTime(vm.Geburtsdatum.Value.Date),
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                Telefon = string.IsNullOrWhiteSpace(vm.Telefon) ? null : vm.Telefon.Trim()
            };

            var res = await client.PostAsJsonAsync("api/antraege/reisepass/step1", payload, ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!res.IsSuccessStatusCode)
                return View("ReisepassStep1", await AddModelErrorsAndReturn(vm, res, ct));

            var idObj = await res.Content.ReadFromJsonAsync<ApiIdResponse>(cancellationToken: ct);
            if (idObj is null || idObj.Id == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty, "Unerwartete Antwort der API.");
                return View("ReisepassStep1", vm);
            }

            return RedirectToAction(nameof(ReisepassStep2), new { id = idObj.Id });
        }

        [HttpGet]
        public async Task<IActionResult> ReisepassStep2(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/antraege/reisepass/{id}", ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var detail = await res.Content.ReadFromJsonAsync<ReisepassDetailResponse>(cancellationToken: ct);
            if (detail is null) return NotFound();

            var vm = new ReisepassStep2Vm
            {
                Id = id,
                AntragstellerName = $"{detail.Vorname} {detail.Nachname}",
                Geburtsdatum = detail.Geburtsdatum.ToDateTime(TimeOnly.MinValue),
                Express = detail.Express ?? false,
                AltpassVorhanden = detail.AltpassVorhanden ?? false
            };

            return View("ReisepassStep2", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReisepassStep2(Guid id, ReisepassStep2Vm vm, string? submitAction, CancellationToken ct)
        {
            if (id == Guid.Empty || vm.Id == Guid.Empty || id != vm.Id)
                return BadRequest();

            var client = _cf.CreateClient("BuergerPortalApi");

            var payload = new ReisepassStep2Request
            {
                Express = vm.Express,
                AltpassVorhanden = vm.AltpassVorhanden,
                Hinweis = string.IsNullOrWhiteSpace(vm.Hinweis) ? null : vm.Hinweis.Trim()
            };

            var put = await client.PutAsJsonAsync($"api/antraege/reisepass/{id}/step2", payload, ct);

            if (put.StatusCode == HttpStatusCode.Unauthorized) return Challenge();

            if (!put.IsSuccessStatusCode)
                return View("ReisepassStep2", await AddModelErrorsAndReturn(vm, put, ct));

            if (string.Equals(submitAction, "submit", StringComparison.OrdinalIgnoreCase))
            {
                var submit = await client.PostAsync($"api/antraege/reisepass/{id}/submit", content: null, ct);

                if (submit.StatusCode == HttpStatusCode.Unauthorized) return Challenge();

                if (!submit.IsSuccessStatusCode)
                    return View("ReisepassStep2", await AddModelErrorsAndReturn(vm, submit, ct));

                TempData["AntragSuccess"] = "Reisepassantrag eingereicht.";
                return RedirectToAction("Status", "Antraege");
            }

            TempData["AntragInfo"] = "Angaben gespeichert. Du kannst jetzt einreichen.";
            return RedirectToAction(nameof(ReisepassStep2), new { id });
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
