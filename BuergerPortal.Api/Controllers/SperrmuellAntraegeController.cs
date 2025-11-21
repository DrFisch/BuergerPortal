using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs;
using BuergerPortal.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/antraege/sperrmuell")]
    [AllowAnonymous]
    [Produces("application/json")]
    public sealed class SperrmuellAntraegeController : ControllerBase
    {
        private readonly ISperrmuellAntragBusinessService _service;

        public SperrmuellAntraegeController(ISperrmuellAntragBusinessService service)
            => _service = service;

        /// <summary>Step 1: legt einen Sperrmüll-Entwurf an und gibt die Id zurück.</summary>
        [HttpPost("step1")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStep1([FromBody] SperrmuellStep1Dto dto, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.CreateStep1Async(dto, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            // 201 Created + Location Header auf GET /{id}
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
        }

        /// <summary>Step 2: ergänzt Adresse, Mengen und Wunschzeit zum bestehenden Entwurf.</summary>
        [HttpPut("{id:guid}/step2")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStep2([FromRoute] Guid id, [FromBody] SperrmuellStep2Dto dto, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.UpdateStep2Async(id, dto, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            return NoContent();
        }

        /// <summary>Reicht den Sperrmüllantrag ein (Status: Eingereicht).</summary>
        [HttpPost("{id:guid}/submit")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Submit([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.SubmitAsync(id, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            return NoContent();
        }

        /// <summary>Detail eines Sperrmüllantrags (nur eigener Antrag).</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SperrmuellDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.GetAsync(id, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            return Ok(result.Value);
        }

        /// <summary>Liste aller eigenen Sperrmüllanträge (Summary).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<SperrmuellSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var list = await _service.GetAllForUserAsync(userId, ct);
            return Ok(list);
        }

        // --- Helpers -------------------------------------------------------------

        private Guid GetUserIdOrThrow()
        {
            var val = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub"); // OIDC-Standard-Claim
            if (string.IsNullOrWhiteSpace(val))
            {
                throw new UnauthorizedAccessException("Kein Benutzerkontext vorhanden.");
            }

            return Guid.Parse(val);
        }

        private IActionResult ToProblem<T>(Result<T> result)
        {
            var (status, title) = result.ErrorCode switch
            {
                ErrorCodes.Validation => (StatusCodes.Status400BadRequest, "Validierungsfehler"),
                ErrorCodes.NotFound => (StatusCodes.Status404NotFound, "Nicht gefunden"),
                ErrorCodes.Forbidden => (StatusCodes.Status403Forbidden, "Zugriff verweigert"),
                _ => (StatusCodes.Status400BadRequest, "Fehler")
            };

            return Problem(
                detail: result.ErrorMessage,
                statusCode: status,
                title: title
            );
        }
    }
}
