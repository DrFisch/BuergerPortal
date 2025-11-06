using BuergerPortal.Api.Contracts.Appointments;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public sealed class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentBusinessService _svc;

        public AppointmentsController(IAppointmentBusinessService svc) => _svc = svc;

        // ---------- Create (201/400/409) ----------
        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Guid>> Create([FromBody] AppointmentCreateRequest req, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var sub = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var userId))
                return Problem("Ungültiges Token (sub).", statusCode: StatusCodes.Status401Unauthorized);

            var dto = new AppointmentCreateDto
            {
                Service = req.Service,
                Location = req.Location,
                StartUtc = req.StartUtc,
                EndUtc = req.EndUtc,
                AntragId=req.AntragId
            };

            var result = await _svc.BookAsync(dto, userId, ct);

            // Erfolg -> 201 Created; Fehler -> ProblemDetails gemäß ErrorCodes
            return FromResult(result, id =>
                new CreatedAtActionResult(nameof(GetById), null, new { id }, id));
        }

        // ---------- Eigene Termine (200) ----------
        [HttpGet("mine")]
        [ProducesResponseType(typeof(IEnumerable<AppointmentListItemResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<AppointmentListItemResponse>>> GetMine(CancellationToken ct)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(sub, out var userId))
                return Problem("Ungültiges Token (sub).", statusCode: StatusCodes.Status401Unauthorized);

            var dtos = await _svc.GetAllForUserAsync(userId, ct);

            var resp = dtos.Select(x => new AppointmentListItemResponse
            {
                Id = x.Id,
                Service = x.Service,
                Location = x.Location,
                StartUtc = DateTime.SpecifyKind(x.StartUtc, DateTimeKind.Utc),
                EndUtc = DateTime.SpecifyKind(x.EndUtc, DateTimeKind.Utc),
                Cancelled = x.Cancelled,
                AntragId = x.AntragId

            });

            return Ok(resp);
        }

        // ---------- Placeholder GetById (200/404 später) ----------
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetById(Guid id)
        {
            return Ok(new { id }); // TODO: echten Read-UseCase einbauen
        }

        // ---------- Busy-Slots (200) ----------
        [HttpGet("busy")]
        [ProducesResponseType(typeof(IEnumerable<BusySlotResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<BusySlotResponse>>> GetBusy([FromQuery] DateOnly date, CancellationToken ct)
        {
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

        // ---------- Cancel (204/400/403/404) ----------
        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Problem("Ungültiges Token (sub).", statusCode: StatusCodes.Status401Unauthorized);

            var result = await _svc.CancelAsync(id, userId, ct);
            return FromResult(result, () => NoContent());
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Problem("Ungültiges Token (sub).", statusCode: StatusCodes.Status401Unauthorized);

            var result = await _svc.DeleteAsync(id, userId, ct);
            return FromResult(result, () => NoContent());
        }

        // ============================================================
        // Einheitliches Mapping: Result<T> -> HTTP
        // ============================================================

        // Für Endpoints mit Rückgabewert (z. B. Create -> Guid)
        private ActionResult<T> FromResult<T>(Result<T> r, Func<T, ActionResult<T>> onOk)
        {
            if (r.IsSuccess)
                return onOk(r.Value!);

            var problem = MapProblem(r.ErrorCode, r.ErrorMessage);
            return problem; // ObjectResult ist kompatibel mit ActionResult<T>
        }

        // Für Endpoints ohne Rückgabewert (z. B. Cancel/Delete -> NoContent)
        private IActionResult FromResult<T>(Result<T> r, Func<IActionResult> onOk)
        {
            if (r.IsSuccess)
                return onOk();

            return MapProblem(r.ErrorCode, r.ErrorMessage);
        }

        private ObjectResult MapProblem(string? code, string? message)
        {
            return (code) switch
            {
                ErrorCodes.NotFound => Problem(message, statusCode: StatusCodes.Status404NotFound),
                ErrorCodes.Forbidden => Problem(message, statusCode: StatusCodes.Status403Forbidden),
                ErrorCodes.SlotConflict => Problem(message, statusCode: StatusCodes.Status409Conflict),
                ErrorCodes.Validation => Problem(message, statusCode: StatusCodes.Status400BadRequest),
                _ => Problem(message ?? "Fehler", statusCode: StatusCodes.Status400BadRequest)
            };
        }
        private bool TryGetUserId(out Guid userId)
        {
            userId = Guid.Empty;
            var sub = User.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out userId);
        }
    }
}
