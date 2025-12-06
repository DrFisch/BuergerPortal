using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Database.Repositories
{
    public sealed class SperrmuellRepository : ISperrmuellRepository
    {
        private readonly PortalDbContext _db;
        public SperrmuellRepository(PortalDbContext dbContext)
        {
            _db = dbContext;
        }
        public async Task AddAsync(SperrmuellAntrag entity, CancellationToken ct)
        {
            _db.SperrmuellAntraege.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct)
        {
            return await _db.Antraege
            .AnyAsync(x => x.Id == id && x.ApplicantUserId == userId, ct);
        }

        public async Task<IReadOnlyList<SperrmuellAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct)
        {
            return await _db.SperrmuellAntraege
            .AsNoTracking()
            .Where(x => x.ApplicantUserId == userId)
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(ct);
        }

        public async Task<SperrmuellAntrag?> GetAsync(Guid id, CancellationToken ct)
        {
            return await _db.SperrmuellAntraege
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task UpdateAsync(SperrmuellAntrag entity, CancellationToken ct)
        {
            _db.SperrmuellAntraege.Update(entity);

            try
            {
                await _db.SaveChangesAsync(ct); // speichert hier
            }
            catch (DbUpdateConcurrencyException)
            {
                throw; // optional: selbst mappen
            }
        }
    }
}
