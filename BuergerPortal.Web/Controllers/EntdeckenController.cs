using BuergerPortal.Web.Features.Entdecken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class EntdeckenController : Controller
    {
        private readonly IHttpClientFactory _cf;

        public EntdeckenController(IHttpClientFactory cf)
        {
            _cf = cf;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var client = _cf.CreateClient("BuergerPortalApi");

            // API Abruf
            var res = await client.GetAsync("api/pois", ct);
            res.EnsureSuccessStatusCode();

            var apiItems = await res.Content.ReadFromJsonAsync<List<PoiListItemVm>>(cancellationToken: ct)
                           ?? new();

            var vm = new EntdeckenIndexVm
            {
                Orte = apiItems 
            };

            return View(vm);
        }
    }
}
