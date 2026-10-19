using BuergerPortal.BundId;
using BuergerPortal.Web.Extensions;
using BuergerPortal.Web.Features.Antraege.Meldebescheinigung.Contracts;
using BuergerPortal.Web.Features.Antraege.Meldebescheinigung.ViewModels;
using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// Meldebescheinigung (2026-10-18), zwei Schritte: „Angaben“ (Art, Zweck; Person und Anschrift aus der BundID) und
    /// „Prüfen und absenden“. Absenden verlangt das Niveau „hoch“ – reicht die Anmeldung nicht, folgt ein erklärter
    /// Step-up mit acr_values=STORK-QAA-Level-4 (im Simulator: Identifizierung mit eID).
    /// </summary>
    [Authorize]
    public class MeldebescheinigungAntraegeController(IHttpClientFactory cf, HttpCurrentUserService currentUser,
        IStringLocalizer<MeldebescheinigungAntraegeController> t) : Controller
    {
        private const string Api = "api/antraege/meldebescheinigung";

        [HttpGet]
        public IActionResult NeueMeldebescheinigung() =>
            View("MeldebescheinigungAngaben", new MeldebescheinigungAngabenVm { Email = currentUser.GetBundIdUser()?.Email, Person = Person() });

        // Entwurf weiter bearbeiten
        [HttpGet]
        public async Task<IActionResult> MeldebescheinigungAngaben(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();
            var (d, fehler) = await LadenAsync(id, ct);
            if (d is null) return fehler!;
            if (d.Status != 1) return RedirectToAction("MeldebescheinigungAntrag", "Antraege", new { id });

            return View("MeldebescheinigungAngaben", new MeldebescheinigungAngabenVm
            {
                Id = d.Id,
                Art = d.Art,
                Zweck = d.Zweck,
                Email = d.Email,
                Person = Person(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MeldebescheinigungAngaben(MeldebescheinigungAngabenVm vm, string? submitAction,
            CancellationToken ct)
        {
            // Person und Anschrift immer aus der aktuellen Anmeldung, nie aus dem Formular
            vm.Person = Person();
            if (!vm.Person.Vollstaendig)
                ModelState.AddModelError(string.Empty, t["Ihre BundID liefert nicht alle nötigen Angaben (Name, Geburtsdatum, Anschrift)."].Value);
            if (!ModelState.IsValid) return View("MeldebescheinigungAngaben", vm);

            var payload = new MeldebescheinigungRequest
            {
                Vorname = vm.Person.Vorname,
                Nachname = vm.Person.Nachname,
                Geburtsdatum = vm.Person.Geburtsdatum!.Value,
                Geburtsort = vm.Person.Geburtsort,
                Geburtsname = vm.Person.Geburtsname,
                Strasse = vm.Person.Strasse!,
                PLZ = vm.Person.PLZ!,
                Ort = vm.Person.Ort!,
                Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
                Art = vm.Art,
                Zweck = string.IsNullOrWhiteSpace(vm.Zweck) ? null : vm.Zweck.Trim(),
            };

            var client = cf.CreateClient("BuergerPortalApi");
            var res = vm.Id is { } vorhanden && vorhanden != Guid.Empty
                ? await client.PutAsJsonAsync($"{Api}/{vorhanden}", payload, ct)
                : await client.PostAsJsonAsync(Api, payload, ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!res.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, t["Die Angaben ließen sich nicht speichern. Bitte versuchen Sie es noch einmal."].Value);
                return View("MeldebescheinigungAngaben", vm);
            }

            var id = vm.Id ?? Guid.Empty;
            if (id == Guid.Empty)
            {
                var neu = await res.Content.ReadFromJsonAsync<ApiIdResponse>(cancellationToken: ct);
                if (neu is null || neu.Id == Guid.Empty) return StatusCode(502);
                id = neu.Id;
            }

            if (submitAction == "entwurf")
            {
                TempData["AntragSuccess"] = t["Entwurf gespeichert – Sie finden ihn unter „Anträge“."].Value;
                return RedirectToAction("Index", "Antraege");
            }
            return RedirectToAction(nameof(MeldebescheinigungPruefen), new { id });
        }

        // Schritt 2 „Prüfen und absenden“
        [HttpGet]
        public async Task<IActionResult> MeldebescheinigungPruefen(Guid id, bool stepup, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();
            // Rückkehr vom Step-up: TempData ginge mit der neuen Portal-Sitzung verloren (E-012) – daher der Parameter
            ViewData["StepUpErfolgreich"] = stepup && Niveau() >= TrustLevel.High;

            var (d, fehler) = await LadenAsync(id, ct);
            if (d is null) return fehler!;
            if (d.Status != 1) return RedirectToAction("MeldebescheinigungAntrag", "Antraege", new { id });

            return View("MeldebescheinigungPruefen", new MeldebescheinigungPruefenVm
            {
                Id = d.Id,
                Person = AusAntrag(d),
                Art = d.Art,
                Zweck = d.Zweck,
                Email = d.Email,
                Niveau = Niveau(),
                NiveauNoetig = TrustLevel.High,
            });
        }

        // Verbindlich absenden – unter „hoch“ zuerst der Step-up, danach zurück zu „Prüfen“
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MeldebescheinigungEinreichen(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            if (Niveau() < TrustLevel.High)
            {
                var properties = new AuthenticationProperties
                {
                    RedirectUri = Url.Action(nameof(MeldebescheinigungPruefen), new { id, stepup = true })
                };
                properties.Items["acr_values"] = TrustLevel.ToStork(TrustLevel.High);
                return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
            }

            var submit = await cf.CreateClient("BuergerPortalApi").PostAsync($"{Api}/{id}/submit", content: null, ct);
            if (submit.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!submit.IsSuccessStatusCode)
            {
                TempData["BookingError"] = t["Der Antrag ließ sich nicht absenden. Bitte versuchen Sie es noch einmal."].Value;
                return RedirectToAction(nameof(MeldebescheinigungPruefen), new { id });
            }

            TempData["AntragSuccess"] = t["Antrag auf eine Meldebescheinigung eingereicht."].Value;
            TempData.MerkePostkorbStatus(submit);
            return RedirectToAction("MeldebescheinigungAntrag", "Antraege", new { id });
        }

        // -------- Helpers ----------

        private int Niveau() => BundIdUser.FromPrincipal(User).TrustLevel;

        private MeldePerson Person()
        {
            var u = currentUser.GetBundIdUser();
            return u is null ? new MeldePerson() : new MeldePerson
            {
                Vorname = u.GivenName ?? string.Empty,
                Nachname = u.FamilyName ?? string.Empty,
                Geburtsdatum = u.Birthdate,
                Geburtsort = u.PlaceOfBirth,
                Geburtsname = u.BirthName,
                Strasse = u.Address?.StreetAddress,
                PLZ = u.Address?.PostalCode,
                Ort = u.Address?.Locality,
            };
        }

        internal static MeldePerson AusAntrag(MeldebescheinigungDetailResponse d) => new()
        {
            Vorname = d.Vorname,
            Nachname = d.Nachname,
            Geburtsdatum = d.Geburtsdatum,
            Geburtsort = d.Geburtsort,
            Geburtsname = d.Geburtsname,
            Strasse = d.Strasse,
            PLZ = d.PLZ,
            Ort = d.Ort,
        };

        private async Task<(MeldebescheinigungDetailResponse? Antrag, IActionResult? Fehler)> LadenAsync(Guid id, CancellationToken ct)
        {
            var res = await cf.CreateClient("BuergerPortalApi").GetAsync($"{Api}/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return (null, Challenge());
            if (!res.IsSuccessStatusCode)
                return (null, StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct)));
            var d = await res.Content.ReadFromJsonAsync<MeldebescheinigungDetailResponse>(cancellationToken: ct);
            return d is null ? (null, NotFound()) : (d, null);
        }
    }
}
