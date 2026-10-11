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
    // Vorher ohne [Authorize]: GET {id} lieferte jede Meldung samt Melder-ID ohne Anmeldung (API nur intern erreichbar).
    [Authorize]
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

            byte[]? foto = null;
            if (!string.IsNullOrEmpty(req.FotoBase64))
            {
                if (!MangelFotoPruefung.TryDecode(req.FotoBase64, req.FotoContentType, out var daten, out var fehler))
                {
                    ModelState.AddModelError("Foto", fehler);
                    return ValidationProblem(ModelState);
                }
                foto = daten;
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
            if (foto != null)
            {
                _db.MaengelFotos.Add(new MangelFoto { MaengelmeldungId = entity.Id, ContentType = req.FotoContentType!, Daten = foto });
            }
            await _db.SaveChangesAsync(ct);

            var resp = new CreateMaengelmeldungResponse
            {
                Id = entity.Id,
                CreatedUtc = entity.CreatedUtc,
                Status = entity.Status
            };

            return Created($"/api/maengelmeldungen/{entity.Id}", resp);
        }

        // Eigene Meldungen, neueste zuerst ("Meine Meldungen" im Portal)
        [HttpGet("mine")]
        public async Task<ActionResult<IReadOnlyList<MaengelmeldungResponse>>> GetMine(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var meldungen = await _db.Maengelmeldungen.AsNoTracking()
                .Where(x => x.ReporterUserId == userId)
                .OrderByDescending(x => x.CreatedUtc)
                .Select(x => new { Meldung = x, HatFoto = _db.MaengelFotos.Any(f => f.MaengelmeldungId == x.Id) })
                .ToListAsync(ct);
            return Ok(meldungen.Select(m => ToResponse(m.Meldung, m.HatFoto)).ToList());
        }

        // Foto der eigenen Meldung
        [HttpGet("{id:guid}/foto")]
        public async Task<IActionResult> GetFoto(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var foto = await _db.MaengelFotos.AsNoTracking()
                .Where(f => f.MaengelmeldungId == id && _db.Maengelmeldungen.Any(m => m.Id == id && m.ReporterUserId == userId))
                .FirstOrDefaultAsync(ct);
            if (foto is null) return NotFound();
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(foto.Daten, foto.ContentType);
        }

        // Nur die eigene Meldung (sonst 404 – ob eine fremde Meldung existiert, verrät die API nicht).
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<MaengelmeldungResponse>> GetById(Guid id, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var e = await _db.Maengelmeldungen.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.ReporterUserId == userId, ct);
            if (e is null) return NotFound();
            return Ok(ToResponse(e, await _db.MaengelFotos.AnyAsync(f => f.MaengelmeldungId == id, ct)));
        }

        private static MaengelmeldungResponse ToResponse(Maengelmeldung e, bool hatFoto) => new(e.Id,
            DateTime.SpecifyKind(e.CreatedUtc, DateTimeKind.Utc), e.Titel, e.Beschreibung, e.Latitude, e.Longitude,
            e.AddressHint, e.Status, hatFoto);

        private bool TryGetUserId(out Guid userId) =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out userId);
    }
}
