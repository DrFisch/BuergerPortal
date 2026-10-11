using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Application.Interfaces.BusinessServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    /// <summary>Zeitleiste eines eigenen Antrags (Reisepass oder Sperrmüll): jeder Statuswechsel mit Zeitpunkt.</summary>
    [ApiController]
    [Route("api/antraege")]
    [Authorize]
    [Produces("application/json")]
    public sealed class AntragVerlaufController(IAntragStatusService status) : ControllerBase
    {
        [HttpGet("{id:guid}/verlauf")]
        [ProducesResponseType(typeof(IReadOnlyList<AntragStatusEreignisDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IReadOnlyList<AntragStatusEreignisDto>>> Get(Guid id, CancellationToken ct)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId))
            {
                return Unauthorized();
            }
            var verlauf = await status.GetVerlaufAsync(id, userId, ct);
            return verlauf is null ? NotFound() : Ok(verlauf);
        }
    }
}
