using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AppointmentsController(IAppointmentRepository repo) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Guid>> Create([FromBody] Appointment appointment, CancellationToken ct)
        {
            if (appointment.StartUtc >= appointment.EndUtc)
                return BadRequest("Startzeit muss vor Endzeit liegen.");

            if (await repo.ExistsOverlapAsync(appointment.UserId, appointment.StartUtc, appointment.EndUtc, ct))
                return Conflict("Der Benutzer hat bereits einen Termin in diesem Zeitraum.");

            appointment.Id = Guid.NewGuid();

            await repo.CreateAsync(appointment, ct);
            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment.Id);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Appointment>> GetById([FromServices] BuergerPortal.Infrastructure.Persistence.AppDbContext db, Guid id, CancellationToken ct)
        {
            var found = await db.Appointments.FindAsync([id], ct);
            return found is null ? NotFound() : Ok(found);
        }
    }
}
