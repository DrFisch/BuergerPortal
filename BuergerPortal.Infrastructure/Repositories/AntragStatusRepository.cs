using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Infrastructure.Repositories
{
    public sealed class AntragStatusRepository(PortalDbContext db) : IAntragStatusRepository
    {
        public async Task<bool> SetStatusAsync(Guid antragId, AntragStatus status, DateTime zeitpunktUtc,
            string? postkorbHandle, CancellationToken ct)
        {
            var antrag = await db.Antraege.FirstOrDefaultAsync(a => a.Id == antragId, ct);
            if (antrag is null)
            {
                return false;
            }
            antrag.Status = status;
            if (!string.IsNullOrWhiteSpace(postkorbHandle))
            {
                antrag.PostkorbHandle = postkorbHandle;
            }
            db.AntragStatusEreignisse.Add(new AntragStatusEreignis
            {
                Id = Guid.NewGuid(),
                AntragId = antragId,
                Status = status,
                ZeitpunktUtc = zeitpunktUtc,
            });
            await db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<IReadOnlyList<AntragStatusEreignis>> GetVerlaufAsync(Guid antragId, CancellationToken ct) =>
            await db.AntragStatusEreignisse.AsNoTracking()
                .Where(e => e.AntragId == antragId)
                .OrderBy(e => e.ZeitpunktUtc)
                .ToListAsync(ct);

        public Task<bool> GehoertPersonAsync(Guid antragId, Guid userId, CancellationToken ct) =>
            db.Antraege.AnyAsync(a => a.Id == antragId && a.ApplicantUserId == userId, ct);

        public async Task<IReadOnlyList<Antrag>> GetFaelligeAsync(AntragStatus status, DateTime vorUtc, CancellationToken ct) =>
            await db.Antraege.AsNoTracking()
                .Where(a => a.Status == status
                            && db.AntragStatusEreignisse.Any(e => e.AntragId == a.Id && e.Status == status)
                            && db.AntragStatusEreignisse.Where(e => e.AntragId == a.Id).Max(e => e.ZeitpunktUtc) <= vorUtc)
                .ToListAsync(ct);
    }
}
