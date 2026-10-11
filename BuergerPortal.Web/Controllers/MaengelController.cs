using BuergerPortal.Web.Features.Maengel.ViewModels;
using BuergerPortal.Web.Features.Maengel.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    public class MaengelController : Controller
    {
        private readonly IHttpClientFactory _cf;

        public MaengelController(IHttpClientFactory cf)
        {
            _cf = cf;
        }

        // Meine Meldungen (nur die eigenen, mit Foto)
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var res = await _cf.CreateClient("BuergerPortalApi").GetAsync("api/maengelmeldungen/mine", ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized) return Challenge();
            res.EnsureSuccessStatusCode();
            var meldungen = await res.Content.ReadFromJsonAsync<List<MaengelmeldungResponse>>(cancellationToken: ct) ?? [];
            return View(meldungen);
        }

        // Foto der eigenen Meldung (die API prüft den Besitz)
        [HttpGet]
        public async Task<IActionResult> Foto(Guid id, CancellationToken ct)
        {
            var res = await _cf.CreateClient("BuergerPortalApi").GetAsync($"api/maengelmeldungen/{id}/foto", ct);
            if (!res.IsSuccessStatusCode) return NotFound();
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(await res.Content.ReadAsByteArrayAsync(ct), res.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateMangelVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Create(CreateMangelVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // Foto: im Browser schon verkleinert; ohne Skript kann es ein großes Kamerabild sein
            string? fotoBase64 = null;
            if (vm.Foto is { Length: > 0 })
            {
                if (vm.Foto.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(vm.Foto), "Das Foto ist zu groß (höchstens 2 MB). Bitte ein kleineres wählen.");
                    return View(vm);
                }
                using var stream = new MemoryStream();
                await vm.Foto.CopyToAsync(stream, ct);
                fotoBase64 = Convert.ToBase64String(stream.ToArray());
            }

            if (vm.Latitude is null || vm.Longitude is null)
            {
                ModelState.AddModelError(nameof(vm.Latitude), "Bitte markieren Sie den Standort auf der Karte.");
                ModelState.AddModelError(nameof(vm.Longitude), "Bitte markieren Sie den Standort auf der Karte.");
                return View(vm);
            }

            var client = _cf.CreateClient("BuergerPortalApi");

            var req = new
            {
                Titel = vm.Titel,
                Beschreibung = vm.Beschreibung,
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                AddressHint = vm.AddressHint,
                FotoBase64 = fotoBase64,
                FotoContentType = fotoBase64 == null ? null : vm.Foto!.ContentType,
            };

            var res = await client.PostAsJsonAsync("api/maengelmeldungen", req, ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized)
                return Challenge();

            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == HttpStatusCode.Unauthorized)
                    return Challenge();

                if (res.StatusCode == HttpStatusCode.BadRequest)
                {
                    try
                    {
                        var problem = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: ct);

                        if (problem?.Errors is not null)
                        {
                            foreach (var kv in problem.Errors)
                            {
                                var field = kv.Key;
                                foreach (var error in kv.Value)
                                {
                                    ModelState.AddModelError(field, error);
                                }
                            }

                            return View(vm);
                        }
                    }
                    catch
                    {
                    }
                }

                var message = await res.Content.ReadAsStringAsync(ct);
                ModelState.AddModelError(string.Empty,
                    $"Fehler beim Senden der Mängelmeldung ({(int)res.StatusCode}). Bitte später erneut versuchen.");

                return View(vm);
            }


            var created = await res.Content.ReadFromJsonAsync<CreateMaengelmeldungResponse>(cancellationToken: ct);

            TempData["Meldung"] = created is null
                ? "Mängelmeldung wurde erstellt. Die Stadt dankt Ihnen für den Hinweis und kümmert sich um das Problem."
                : $"Mängelmeldung wurde erstellt. Die Stadt dankt Ihnen für den Hinweis und kümmert sich um das Problem. Nummer: {created.Id.ToString()[..8]}";

            return RedirectToAction(nameof(Create));
        }

        private sealed class CreateMaengelmeldungResponse
        {
            public Guid Id { get; set; }
            public DateTime CreatedUtc { get; set; }
            public string Status { get; set; } = string.Empty;
        }
    }
}
