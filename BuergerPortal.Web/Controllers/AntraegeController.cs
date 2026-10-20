
using BuergerPortal.Web.Features.Antraege;
using BuergerPortal.Web.Features.Antraege.Meldebescheinigung.Contracts;
using BuergerPortal.Web.Features.Antraege.Meldebescheinigung.ViewModels;
using BuergerPortal.Web.Features.Antraege.Reisepass;
using BuergerPortal.Web.Helper;
using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    public class AntraegeController : Controller
    {
        private readonly IHttpClientFactory _cf;

        // Meldungen: deutscher Text ist Schlüssel und Rückfall, Englisch in Resources/Controllers/AntraegeController.en.resx
        private readonly IStringLocalizer<AntraegeController> _t;

        public AntraegeController(IHttpClientFactory cf, IStringLocalizer<AntraegeController> t)
        {
            _cf = cf;
            _t = t;
        }

        // Übersicht (2026-10-18): eigene Anträge, angefangene Entwürfe und der Leistungskatalog mit Niveau, Gebühr, Dauer
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var antraege = await EigeneAntraegeAsync(ct);
            if (antraege is null) return Challenge();

            var user = BundIdUser.FromPrincipal(User);
            return View(new AntraegeUebersichtVm
            {
                Antraege = antraege,
                Niveau = user.TrustLevel,
                Geburtsdatum = user.Birthdate,
            });
        }

        // Eigenen Entwurf verwerfen (eingereichte Anträge lehnt die API mit 409 ab)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verwerfen(Guid id, string? zurueck, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();
            var res = await _cf.CreateClient("BuergerPortalApi").DeleteAsync($"api/antraege/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();

            TempData[res.IsSuccessStatusCode ? "AntragSuccess" : "BookingError"] = res.IsSuccessStatusCode
                ? _t["Entwurf verworfen."].Value
                : _t["Der Entwurf ließ sich nicht verwerfen – vielleicht ist er schon eingereicht."].Value;
            return zurueck == "status"
                ? RedirectToAction(nameof(Status), new { filter = "entwuerfe" })
                : RedirectToAction(nameof(Index));
        }

        /// <summary>Alle eigenen Anträge aller Arten als Zeilen, neueste zuerst; null, wenn die API 401 meldet.
        /// Gemeinsam mit der Startseite „Heute“ von Portal 2 (Features/Antraege/EigeneAntraege.cs).</summary>
        private Task<List<AntragZeileVm>?> EigeneAntraegeAsync(CancellationToken ct) =>
            EigeneAntraege.LadenAsync(_cf.CreateClient("BuergerPortalApi"), ct);

        // „Meine Anträge“ (2026-10-18): alle Arten in einer Liste, Filter alle/laufend/entwuerfe/erledigt
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Status(string? filter, CancellationToken ct)
        {
            var antraege = await EigeneAntraegeAsync(ct);
            if (antraege is null) return Challenge();
            return View("Status", new AntraegeStatusVm { Antraege = antraege, Filter = AntraegeStatusVm.Normalisieren(filter) });
        }

        // -------- Reisepass-Detail 

        [HttpGet]
        public async Task<IActionResult> Antrag(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/antraege/reisepass/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var d = await res.Content.ReadFromJsonAsync<ReisepassDetailResponse>(cancellationToken: ct);
            if (d is null) return NotFound();

            var map = AntragStatusUi.Map(d.Status);
            var verlauf = await VerlaufAsync(client, id, ct);
            var vm = new AntragDetailVm
            {
                Id = d.Id,
                Antragsteller = $"{d.Vorname} {d.Nachname}",
                Geburtsdatum = d.Geburtsdatum.ToDateTime(TimeOnly.MinValue),
                Status = d.Status,
                StatusText = map.text,
                BadgeClass = map.badge,
                ProgressPercent = map.progress,
                Express = d.Express ?? false,
                AltpassVorhanden = d.AltpassVorhanden ?? false,
                Hinweis = d.Hinweis,
                CreatedUtc = d.CreatedUtc.ToBerlin(),
                SubmittedUtc = d.SubmittedUtc?.ToBerlin(),
                InBearbeitungSeit = verlauf.InBearbeitung,
                AbgeschlossenAm = verlauf.Abgeschlossen,
                Email = d.Email,
                Telefon = d.Telefon
            };

            return View("AntragDetail", vm);
        }

        //Sperrmüll-Detail 

        [HttpGet]
        public async Task<IActionResult> SperrmuellAntrag(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return BadRequest();

            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/antraege/sperrmuell/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var d = await res.Content.ReadFromJsonAsync<SperrmuellDetailResponse>(cancellationToken: ct);
            if (d is null) return NotFound();

            var map = AntragStatusUi.Map(d.Status);
            var verlauf = await VerlaufAsync(client, id, ct);

            var vm = new SperrmuellAntragDetailVm
            {
                Id = d.Id,
                Antragsteller = $"{d.Vorname} {d.Nachname}",
                Geburtsdatum = d.Geburtsdatum.ToDateTime(TimeOnly.MinValue),

                Status = (int)d.Status,
                StatusText = map.text,
                BadgeClass = map.badge,
                ProgressPercent = map.progress,

                CreatedUtc = d.CreatedUtc.ToBerlin(),
                SubmittedUtc = d.SubmittedUtc?.ToBerlin(),
                InBearbeitungSeit = verlauf.InBearbeitung,
                AbgeschlossenAm = verlauf.Abgeschlossen,

                Email = d.Email,
                Telefon = d.Telefon,

                Strasse = d.Strasse,
                PLZ = d.PLZ,
                Ort = d.Ort,

                HolzKubikmeter = d.HolzKubikmeter,
                SonstigesKubikmeter = d.SonstigesKubikmeter,
                Matratzen = d.Matratzen,

                Wunschzeit = d.Wunschzeit.ToLocalTime(),
                Hinweis = d.Hinweis
            };

            return View("SperrmuellAntragDetail", vm);
        }

        // -------- Meldebescheinigung (2026-10-18): Detail und – nach Abschluss – die Bescheinigung als Druckansicht

        [HttpGet]
        public async Task<IActionResult> MeldebescheinigungAntrag(Guid id, CancellationToken ct)
        {
            var (vm, fehler) = await MeldebescheinigungAsync(id, ct);
            return vm is null ? fehler! : View("MeldebescheinigungAntragDetail", vm);
        }

        [HttpGet]
        public async Task<IActionResult> MeldebescheinigungDokument(Guid id, CancellationToken ct)
        {
            var (vm, fehler) = await MeldebescheinigungAsync(id, ct);
            if (vm is null) return fehler!;
            // erst nach Abschluss gibt es eine Bescheinigung
            return vm.Status == 4 ? View("MeldebescheinigungDokument", vm) : RedirectToAction(nameof(MeldebescheinigungAntrag), new { id });
        }

        private async Task<(MeldebescheinigungDetailVm? Vm, IActionResult? Fehler)> MeldebescheinigungAsync(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty) return (null, BadRequest());

            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync($"api/antraege/meldebescheinigung/{id}", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return (null, Challenge());
            if (!res.IsSuccessStatusCode)
                return (null, StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct)));

            var d = await res.Content.ReadFromJsonAsync<MeldebescheinigungDetailResponse>(cancellationToken: ct);
            if (d is null) return (null, NotFound());

            var verlauf = await VerlaufAsync(client, id, ct);
            return (new MeldebescheinigungDetailVm
            {
                Id = d.Id,
                Status = d.Status,
                Person = MeldebescheinigungAntraegeController.AusAntrag(d),
                Art = d.Art,
                Zweck = d.Zweck,
                Email = d.Email,
                CreatedUtc = d.CreatedUtc.ToBerlin(),
                SubmittedUtc = d.SubmittedUtc?.ToBerlin(),
                InBearbeitungSeit = verlauf.InBearbeitung,
                AbgeschlossenAm = verlauf.Abgeschlossen,
            }, null);
        }

        // Zeitleiste: wann der Antrag in Bearbeitung ging und wann er abgeschlossen wurde (Statusverlauf der API, in
        // deutscher Zeit). Ältere Anträge haben keinen Verlauf – dann bleiben die Felder leer.
        private static async Task<(DateTime? InBearbeitung, DateTime? Abgeschlossen)> VerlaufAsync(HttpClient client, Guid id,
            CancellationToken ct)
        {
            var res = await client.GetAsync($"api/antraege/{id}/verlauf", ct);
            if (!res.IsSuccessStatusCode) return (null, null);
            var eintraege = await res.Content.ReadFromJsonAsync<List<VerlaufEintrag>>(cancellationToken: ct) ?? [];
            DateTime? Zeit(params int[] status) =>
                eintraege.LastOrDefault(e => status.Contains(e.Status)) is { } e ? e.ZeitpunktUtc.ToBerlin() : null;
            return (Zeit((int)AntragStatusUi.AntragStatus.InPruefung),
                Zeit((int)AntragStatusUi.AntragStatus.Genehmigt, (int)AntragStatusUi.AntragStatus.Abgelehnt));
        }

        private sealed record VerlaufEintrag(int Status, DateTime ZeitpunktUtc);
    }
}
