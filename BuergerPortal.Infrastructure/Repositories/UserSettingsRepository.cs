using BuergerPortal.Application.Interfaces.UserEinstellungen;
using BuergerPortal.Domain.Settings.Entity;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Database.Repositories
{
    public sealed class UserSettingsRepository : IUserSettingsRepository
    {
        private readonly PortalDbContext _db;
        public UserSettingsRepository(PortalDbContext db) => _db = db;

        public Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => _db.UserSettings.SingleOrDefaultAsync(x => x.UserId == userId, ct);

        public async Task CreateAsync(UserSettings entity, CancellationToken ct)
        {
            _db.UserSettings.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(UserSettings entity, CancellationToken ct)
        {
            _db.UserSettings.Update(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
