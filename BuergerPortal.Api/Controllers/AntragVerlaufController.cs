using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Application.Interfaces.BusinessServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    /// <summary>
    /// Für alle Antragsarten: Zeitleiste eines eigenen Antrags (jeder Statuswechsel mit Zeitpunkt) und – seit 2026-10-18 –
    /// eigenen Entwurf verwerfen.
    /// </summary>
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

        /// <summary>Eigenen Entwurf verwerfen. Eingereichte Anträge: 409 – sie lassen sich nicht mehr löschen.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> EntwurfVerwerfen(Guid id, CancellationToken ct)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId))
            {
                return Unauthorized();
            }
            return await status.EntwurfVerwerfenAsync(id, userId, ct) switch
            {
                EntwurfVerwerfenErgebnis.Verworfen => NoContent(),
                EntwurfVerwerfenErgebnis.KeinEntwurf => Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Nur Entwürfe lassen sich verwerfen.",
                    detail: "Der Antrag ist eingereicht und gehört zum Vorgang der Behörde."),
                _ => NotFound(),
            };
        }
    }
}
