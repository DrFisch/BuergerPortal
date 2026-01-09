using BuergerPortal.Api.Contracts.Maengelmeldungen;
using BuergerPortal.Domain.Maengel;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/maengelmeldungen")]
    public sealed class MaengelmeldungenController : ControllerBase
    {
        private readonly PortalDbContext _db;

        public MaengelmeldungenController(PortalDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [Authorize] 
        public async Task<ActionResult<CreateMaengelmeldungResponse>> Create(
            [FromBody] CreateMaengelmeldungRequest req,
            CancellationToken ct)
        {
            
            if (req.Latitude == null || req.Longitude == null)
            {
                ModelState.AddModelError(nameof(req.Latitude), "Latitude darf nicht null sein.");
                ModelState.AddModelError(nameof(req.Longitude), "Longitude darf nicht null sein.");
                return ValidationProblem(ModelState);
            }

            Guid? reporterUserId = null;

            
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (Guid.TryParse(sub, out var guid))
                reporterUserId = guid;

            var entity = new Maengelmeldung
            {
                Id = Guid.NewGuid(),
                ReporterUserId = reporterUserId,
                CreatedUtc = DateTime.UtcNow,
                Titel = req.Titel.Trim(),
                Beschreibung = req.Beschreibung.Trim(),
                Latitude = req.Latitude.Value,
                Longitude = req.Longitude.Value,
                AddressHint = string.IsNullOrWhiteSpace(req.AddressHint) ? null : req.AddressHint.Trim(),
                Status = "Offen"
            };

            _db.Maengelmeldungen.Add(entity);
            await _db.SaveChangesAsync(ct);

            var resp = new CreateMaengelmeldungResponse
            {
                Id = entity.Id,
                CreatedUtc = entity.CreatedUtc,
                Status = entity.Status
            };

            return Created($"/api/maengelmeldungen/{entity.Id}", resp);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Maengelmeldung>> GetById(Guid id, CancellationToken ct)
        {
            var e = await _db.Maengelmeldungen.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (e is null) return NotFound();
            return Ok(e);
        }
    }
}
