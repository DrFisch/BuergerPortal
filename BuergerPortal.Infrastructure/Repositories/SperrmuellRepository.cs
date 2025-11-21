using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Infrastructure.Persistence;
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
            throw new NotImplementedException();
        }

        public async Task<bool> ExistsForUserAsync(Guid id, Guid userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<SperrmuellAntrag>> GetAllForUserAsync(Guid userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<SperrmuellAntrag?> GetAsync(Guid id, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(SperrmuellAntrag entity, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
