using BuergerPortal.Api.Extensions;
using BuergerPortal.Api.Postkorb;
using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Domain.Antrag.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    /// <summary>
    /// Meldebescheinigung (2026-10-18): Entwurf anlegen/ändern mit jedem Niveau, einreichen erst ab BundID-Niveau
    /// „hoch“ (wie beim Online-Dienst des Landes Berlin, der den Online-Ausweis verlangt). Eingangsbestätigung und
    /// später die Bescheinigung selbst gehen ins BundID-Postfach – lesbar ebenfalls erst ab „hoch“.
    /// </summary>
    [ApiController]
    [Route("api/antraege/meldebescheinigung")]
    [Authorize]
    [Produces("application/json")]
    public sealed class MeldebescheinigungAntraegeController(IMeldebescheinigungAntragBusinessService service,
        IPostkorbService postkorb) : ControllerBase
    {
        public const string Leistung = "Meldebescheinigung";

        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] MeldebescheinigungDto dto, CancellationToken ct)
        {
            var result = await service.CreateAsync(dto, UserId(), ct);
            if (!result.IsSuccess) return ToProblem(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
        }

        /// <summary>Entwurf ändern (nur solange er nicht eingereicht ist).</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] MeldebescheinigungDto dto, CancellationToken ct)
        {
            var result = await service.UpdateAsync(id, dto, UserId(), ct);
            return result.IsSuccess ? NoContent() : ToProblem(result);
        }

        [HttpPost("{id:guid}/submit")]
        // Meldedaten sind besonders schutzwürdig: einreichen erst ab BundID-Niveau "hoch" (sonst 403 → Step-up im Portal)
        [Authorize(Policy = TrustLevelPolicies.High)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Submit([FromRoute] Guid id, [FromServices] IAntragStatusService status,
            CancellationToken ct)
        {
            var userId = UserId();
            var result = await service.SubmitAsync(id, userId, ct);
            if (!result.IsSuccess) return ToProblem(result);

            // Zeitleiste und spätere Statusmeldungen: Einreichen festhalten, Postkorb-Handle aus der Anmeldung merken.
            await status.EingereichtAsync(id, User.FindFirstValue(PostkorbBenachrichtigung.PostkorbHandleClaim), ct);

            var antrag = await service.GetAsync(id, userId, ct);
            var art = antrag.IsSuccess && antrag.Value?.Art == MeldebescheinigungArt.Erweitert ? "erweiterte" : "einfache";
            await this.SendPostkorbAsync(postkorb,
                "Eingangsbestätigung: Meldebescheinigung",
                $"""
                {this.Anrede()}

                Ihr Antrag auf eine {art} Meldebescheinigung ist bei uns eingegangen.

                Vorgangsnummer: {id}
                Eingegangen: {PostkorbBenachrichtigung.Ortszeit(DateTime.UtcNow)}

                Die Bescheinigung schicken wir Ihnen nach der Prüfung in dieses Postfach; Sie können sie außerdem im BürgerPortal abrufen und drucken.
                """,
                Leistung, ct, storkQaaLevel: TrustLevelPolicies.HighLevel);

            return NoContent();
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(MeldebescheinigungDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var result = await service.GetAsync(id, UserId(), ct);
            return result.IsSuccess ? Ok(result.Value) : ToProblem(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<MeldebescheinigungSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken ct) => Ok(await service.GetAllForUserAsync(UserId(), ct));

        // Helpers

        private Guid UserId()
        {
            var val = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(val)) throw new UnauthorizedAccessException("Kein Benutzerkontext vorhanden.");
            return Guid.Parse(val);
        }

        private ObjectResult ToProblem<T>(Result<T> result)
        {
            var (code, title) = result.ErrorCode switch
            {
                ErrorCodes.Validation => (StatusCodes.Status400BadRequest, "Validierungsfehler"),
                ErrorCodes.NotFound => (StatusCodes.Status404NotFound, "Nicht gefunden"),
                ErrorCodes.Forbidden => (StatusCodes.Status403Forbidden, "Zugriff verweigert"),
                _ => (StatusCodes.Status400BadRequest, "Fehler")
            };
            return Problem(detail: result.ErrorMessage, statusCode: code, title: title);
        }
    }
}
