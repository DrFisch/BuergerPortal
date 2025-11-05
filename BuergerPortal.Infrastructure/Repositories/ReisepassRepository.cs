using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Repositories
{
    using BuergerPortal.Application.Interfaces.Repositories;
    using BuergerPortal.Domain.Antrag.Entity;
    using BuergerPortal.Infrastructure.Persistence;
    using Microsoft.EntityFrameworkCore;

    public sealed class ReisepassRepository : IReisepassRepository
    {
        private readonly PortalDbContext _db;
        public ReisepassRepository(PortalDbContext db) => _db = db;

        public Task<ReisepassAntrag?> GetAsync(Guid id, CancellationToken ct)
            => _db.ReisepassAntraege.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<IReadOnlyList<ReisepassAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct)
            => await _db.ReisepassAntraege.AsNoTracking()
                   .Where(x => x.ApplicantUserId == userId)
                   .OrderByDescending(x => x.CreatedUtc)
                   .ToListAsync(ct);

        public async Task AddAsync(ReisepassAntrag entity, CancellationToken ct)
        {
            _db.ReisepassAntraege.Add(entity);
            await _db.SaveChangesAsync(ct); // ← speichert hier
        }

        public async Task UpdateAsync(ReisepassAntrag entity, CancellationToken ct)
        {
            _db.ReisepassAntraege.Update(entity);
            try
            {
                await _db.SaveChangesAsync(ct); // ← speichert hier
            }
            catch (DbUpdateConcurrencyException)
            {
                throw; // ggf. hier in eigenes App-Error-Muster mappen
            }
        }

        public Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct)
            => _db.Antraege.AnyAsync(x => x.Id == id && x.ApplicantUserId == userId, ct);
    }

}
