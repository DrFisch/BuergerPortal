using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Infrastructure.Database.Repositories
{
    public sealed class MeldebescheinigungRepository(PortalDbContext db) : IMeldebescheinigungRepository
    {
        public Task<MeldebescheinigungAntrag?> GetAsync(Guid id, CancellationToken ct) =>
            db.MeldebescheinigungAntraege.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<IReadOnlyList<MeldebescheinigungAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct) =>
            await db.MeldebescheinigungAntraege
                .AsNoTracking()
                .Where(x => x.ApplicantUserId == userId)
                .OrderByDescending(x => x.CreatedUtc)
                .ToListAsync(ct);

        public async Task AddAsync(MeldebescheinigungAntrag entity, CancellationToken ct)
        {
            db.MeldebescheinigungAntraege.Add(entity);
            await db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(MeldebescheinigungAntrag entity, CancellationToken ct)
        {
            db.MeldebescheinigungAntraege.Update(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
