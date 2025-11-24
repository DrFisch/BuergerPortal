
using BuergerPortal.Web.Features.Antraege.Reisepass;
using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    public class AntraegeController : Controller
    {
        private readonly IHttpClientFactory _cf;
        public AntraegeController(IHttpClientFactory cf)
        {
            _cf = cf;
        }
        public IActionResult Index()
        {
            return View();
        }
        

        

        
        [HttpGet]
        public async Task<IActionResult> Status(CancellationToken ct)
        {
            var client = _cf.CreateClient("BuergerPortalApi");
            var res = await client.GetAsync("api/antraege/reisepass", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync(ct));

            var items = await res.Content.ReadFromJsonAsync<List<ReisepassSummaryResponse>>(cancellationToken: ct)
                        ?? new();

            var vm = new StatusListeVm
            {
                Items = items.OrderByDescending(x => x.SubmittedUtc ?? x.CreatedUtc)
                 .Select(x => {
                     var s = AntragStatusUi.Map(x.Status);
                     var t = AntragTypUiMap.Map(x.Typ);

                     return new StatusListItemVm
                     {
                         Id = x.Id,
                         Antragsteller = $"{x.Vorname} {x.Nachname}",

                         Typ = x.Typ,
                         TypText = t.text,
                         TypBadgeClass = t.badgeClass,

                         Angelegt = x.CreatedUtc.ToLocalTime(),
                         Eingereicht = x.SubmittedUtc?.ToLocalTime(),

                         StatusText = s.text,
                         Status= x.Status,
                         BadgeClass = s.badge,
                         ProgressPercent = s.progress
                     };
                 }).ToList()
            };

            return View("Status", vm);
        }

        // Detail – zeigt einen Antrag + Button „Termin zur Klärung“
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
            var vm = new AntragDetailVm
            {
                Id = d.Id,
                Antragsteller = $"{d.Vorname} {d.Nachname}",
                Geburtsdatum = d.Geburtsdatum.ToDateTime(TimeOnly.MinValue),
                StatusText = map.text,
                BadgeClass = map.badge,
                ProgressPercent = map.progress,
                Express = d.Express ?? false,
                AltpassVorhanden = d.AltpassVorhanden ?? false,
                Hinweis = d.Hinweis,
                CreatedUtc = d.CreatedUtc.ToLocalTime(),
                SubmittedUtc = d.SubmittedUtc?.ToLocalTime(),
                Email = d.Email,
                Telefon = d.Telefon
            };

            return View("AntragDetail", vm);
        }
        


        
    }
}
