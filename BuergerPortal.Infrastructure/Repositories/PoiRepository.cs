using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Poi.Entity;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Database.Repositories
{
    public sealed class PoiRepository : IPoiRepository
    {
        private readonly PortalDbContext _db;
        public PoiRepository(PortalDbContext db) => _db = db;

        public Task<List<PoiEntity>> GetAllActiveAsync(CancellationToken ct)
        {
            return _db.Pois
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        public Task<PoiEntity?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            return _db.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }
    }
}
