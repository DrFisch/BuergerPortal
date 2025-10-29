using BuergerPortal.Api.Contracts;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentBusinessService _svc;

        public AppointmentsController(IAppointmentBusinessService svc)
            => _svc = svc;

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Guid>> Create([FromBody] AppointmentCreateRequest req, CancellationToken ct)
        {
            var userId = User.FindFirst("sub")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? "demo-user";

            var dto = new AppointmentCreateDto
            {
                Service = req.Service,
                Location = req.Location,
                StartUtc = req.StartUtc,
                EndUtc = req.EndUtc
            };

            var result = await _svc.BookAsync(dto, userId, ct);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);

            // ProblemDetails für schöne Fehlermeldungen
            if (result.ErrorCode == ErrorCodes.SlotConflict)
                return Problem(title: "Zeitslot bereits belegt",
                               detail: "Bitte wählen Sie einen anderen Zeitpunkt.",
                               statusCode: StatusCodes.Status409Conflict);

            return Problem(title: "Ungültige Eingaben",
                           detail: result.ErrorMessage ?? "Bitte Eingaben prüfen.",
                           statusCode: StatusCodes.Status400BadRequest);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetById(Guid id)
        {
            return Ok(new { id }); // implementierst du später mit Read-UseCase
        }
    }
}
