using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/antraege/reisepass")]
    [AllowAnonymous]
    [Produces("application/json")]
    public sealed class ReisepassAntraegeController : ControllerBase
    {
        private readonly IReisepassAntragBusinessService _service;

        public ReisepassAntraegeController(IReisepassAntragBusinessService service)
            => _service = service;

        /// <summary>Step 1: legt einen Entwurf an und gibt die Id zurück.</summary>
        [HttpPost("step1")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStep1([FromBody] ReisepassStep1Dto dto, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.CreateStep1Async(dto, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            // 201 Created + Location Header auf GET /{id}
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });
        }

        /// <summary>Step 2: ergänzt Optionen zum bestehenden Entwurf.</summary>
        [HttpPut("{id:guid}/step2")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStep2([FromRoute] Guid id, [FromBody] ReisepassStep2Dto dto, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.UpdateStep2Async(id, dto, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            return NoContent();
        }

        /// <summary>Reicht den Antrag ein (Status: Eingereicht).</summary>
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

        /// <summary>Detail eines Antrags (nur eigener Antrag).</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ReisepassDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var result = await _service.GetAsync(id, userId, ct);

            if (!result.IsSuccess) return ToProblem(result);

            return Ok(result.Value);
        }

        /// <summary>Liste aller eigenen Reisepassanträge (Summary).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<ReisepassSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var userId = GetUserIdOrThrow();
            var list = await _service.GetAllForUserAsync(userId, ct);
            return Ok(list);
        }

        // --- Helpers -------------------------------------------------------------

        private Guid GetUserIdOrThrow()
        {
            // Versuch 1: NameIdentifier (typisch bei ASP.NET/JWT)
            var val = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub"); // OIDC-Standard-Claim
            if (string.IsNullOrWhiteSpace(val))
            { 
                
                throw new UnauthorizedAccessException("Kein Benutzerkontext vorhanden."); 
            }

            // Dein Modell nutzt Guid als ApplicantUserId
            return Guid.Parse(val);
        }

        private IActionResult ToProblem<T>(Result<T> result)
        {
            // Mapped deine ErrorCodes -> HTTP
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
