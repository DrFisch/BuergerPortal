using BuergerPortal.Web.Features.Maengel.ViewModels;
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

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateMangelVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateMangelVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var client = _cf.CreateClient("BuergerPortalApi");

            // Request DTO passend zur API
            var req = new
            {
                Titel = vm.Titel,
                Beschreibung = vm.Beschreibung,
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                AddressHint = vm.AddressHint
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

                // alle anderen Fehler (500, 403)
                var message = await res.Content.ReadAsStringAsync(ct);
                ModelState.AddModelError(string.Empty,
                    $"Fehler beim Senden der Mängelmeldung ({(int)res.StatusCode}). Bitte später erneut versuchen.");

                return View(vm);
            }


            //Response lesen
            var created = await res.Content.ReadFromJsonAsync<CreateMaengelmeldungResponse>(cancellationToken: ct);

            TempData["Meldung"] = created is null
                ? "Mängelmeldung wurde erstellt. Die Stadt denkt ihnen für diesen Hinweis und wird sich um das Problem kümmern!"
                : $"Mängelmeldung wurde erstellt. Die Stadt denkt ihnen für diesen Hinweis und wird sich um das Problem kümmern! ID: {created.Id}";

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
