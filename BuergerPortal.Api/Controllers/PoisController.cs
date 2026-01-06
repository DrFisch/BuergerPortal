using BuergerPortal.Application.Interfaces.BusinessServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // Jeder darf Orte entdecken
    public sealed class PoisController : ControllerBase
    {
        private readonly IPoiBusinessService _svc;

        public PoisController(IPoiBusinessService svc)
        {
            _svc = svc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PoiResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PoiResponse>>> GetAll(CancellationToken ct)
        {
            var dtos = await _svc.GetDiscoveryListAsync(ct);

            var resp = dtos.Select(x => new PoiResponse
            {
                Id = x.Id,
                Name = x.Name,
                Cat = x.Category,
                Desc = x.Description,
                Tags = x.Tags,
                Icon = x.Icon,
                Lat = x.Latitude,
                Lon = x.Longitude,
                Addr = x.Address
            });

            return Ok(resp);
        }
    }

    // Response Model für das Frontend
    public class PoiResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Cat { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public string? Addr { get; set; }
    }
}
