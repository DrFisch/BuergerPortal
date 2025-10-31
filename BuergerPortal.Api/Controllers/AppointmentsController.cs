using BuergerPortal.Api.Contracts.Appointments;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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
                      ?? throw new UnauthorizedAccessException("Kein Benutzer im Token.");

            // --- Serverseitige Validierung ---
            if (req.EndUtc <= req.StartUtc)
                return Problem(title: "Ungültiger Zeitraum",
                               detail: "Ende muss nach dem Start liegen.",
                               statusCode: StatusCodes.Status400BadRequest);

            var duration = req.EndUtc - req.StartUtc;
            if (duration < TimeSpan.FromMinutes(15) || duration > TimeSpan.FromHours(8))
                return Problem(title: "Ungültige Dauer",
                               detail: "Dauer muss zwischen 15 und 480 Minuten liegen.",
                               statusCode: StatusCodes.Status400BadRequest);

            bool IsQuarterAligned(DateTime dt)
                => dt.Second == 0 && dt.Millisecond == 0 && (dt.Minute % 15) == 0;

            if (!IsQuarterAligned(req.StartUtc) || !IsQuarterAligned(req.EndUtc))
                return Problem(title: "Nur 15-Minuten-Takt erlaubt",
                               detail: "Start und Ende müssen auf :00/:15/:30/:45 liegen.",
                               statusCode: StatusCodes.Status400BadRequest);

            // Optional: Öffnungszeiten in Europe/Berlin (wenn gewünscht)
            // var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            // var startLocal = TimeZoneInfo.ConvertTimeFromUtc(req.StartUtc, tz);
            // var endLocal   = TimeZoneInfo.ConvertTimeFromUtc(req.EndUtc, tz);
            // if (startLocal.Hour < 8 || endLocal.Hour > 18) ...

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

            if (result.ErrorCode == ErrorCodes.SlotConflict)
                return Problem(title: "Zeitslot bereits belegt",
                               detail: "Bitte wählen Sie einen anderen Zeitpunkt.",
                               statusCode: StatusCodes.Status409Conflict);

            return Problem(title: "Ungültige Eingaben",
                           detail: result.ErrorMessage ?? "Bitte Eingaben prüfen.",
                           statusCode: StatusCodes.Status400BadRequest);
        }

        [HttpGet("mine")]
        [ProducesResponseType(typeof(IEnumerable<AppointmentListItemResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<AppointmentListItemResponse>>> GetMine(CancellationToken ct)
        {
            var userId = User.FindFirst("sub")?.Value
                      ?? throw new UnauthorizedAccessException("Kein Benutzer im Token.");

            var dtos = await _svc.GetAllForUserAsync(userId, ct);

            // falls du strikt Contracts zurückgeben willst:
            var resp = dtos.Select(x => new AppointmentListItemResponse
            {
                Id = x.Id,
                Service = x.Service,
                Location = x.Location,
                StartUtc = DateTime.SpecifyKind(x.StartUtc, DateTimeKind.Utc),
                EndUtc = DateTime.SpecifyKind(x.EndUtc, DateTimeKind.Utc),
                Cancelled = x.Cancelled
            });

            return Ok(resp);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetById(Guid id)
        {
            return Ok(new { id }); // implementierst du später mit Read-UseCase
        }
        [HttpGet("busy")]
        [ProducesResponseType(typeof(IEnumerable<BusySlotResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<BusySlotResponse>>> GetBusy([FromQuery] DateOnly date, CancellationToken ct)
        {
            // 08:00–12:00 Europe/Berlin -> in UTC umrechnen
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

            var localStart = new DateTime(date.Year, date.Month, date.Day, 8, 0, 0, DateTimeKind.Unspecified);
            var localEnd = new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Unspecified);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

            var dtos = await _svc.GetBusyAsync(fromUtc, toUtc, ct);

            var resp = dtos.Select(x => new BusySlotResponse
            {
                StartUtc = DateTime.SpecifyKind(x.StartUtc, DateTimeKind.Utc),
                EndUtc = DateTime.SpecifyKind(x.EndUtc, DateTimeKind.Utc)
            });

            return Ok(resp);
        }

        private IActionResult FromResult<T>(Result<T> r, Func<T, IActionResult> onOk)
        {
            if (r.IsSuccess)
                return onOk(r.Value!);

            return r.ErrorCode switch
            {
                ErrorCodes.NotFound => Problem(r.ErrorMessage, statusCode: StatusCodes.Status404NotFound),
                ErrorCodes.Forbidden => Problem(r.ErrorMessage, statusCode: StatusCodes.Status403Forbidden),
                ErrorCodes.SlotConflict => Problem(r.ErrorMessage, statusCode: StatusCodes.Status409Conflict),
                ErrorCodes.Validation => Problem(r.ErrorMessage, statusCode: StatusCodes.Status400BadRequest),
                _ => Problem(r.ErrorMessage ?? "Fehler", statusCode: StatusCodes.Status400BadRequest)
            };
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            var userId = User?.FindFirst("sub")?.Value ?? User?.Identity?.Name ?? string.Empty;
            var result = await _svc.CancelAsync(id, userId, ct);
            return FromResult(result, _ => NoContent());
        }

        // Optional: Hard-Delete
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var userId = User?.FindFirst("sub")?.Value ?? User?.Identity?.Name ?? string.Empty;
            var result = await _svc.DeleteAsync(id, userId, ct);
            return FromResult(result, _ => NoContent());
        }
    }
}
