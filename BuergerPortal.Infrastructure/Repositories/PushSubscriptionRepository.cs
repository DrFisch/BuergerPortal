using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Push.Entity;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Infrastructure.Repositories
{
    public sealed class PushSubscriptionRepository : IPushSubscriptionRepository
    {
        private readonly PortalDbContext _db;
        public PushSubscriptionRepository(PortalDbContext db) => _db = db;

        public async Task SaveAsync(PushSubscription subscription, CancellationToken ct)
        {
            var existing = await _db.PushSubscriptions.SingleOrDefaultAsync(x => x.Endpoint == subscription.Endpoint, ct);
            if (existing is null)
            {
                _db.PushSubscriptions.Add(subscription);
            }
            else
            {
                existing.UserId = subscription.UserId;
                existing.P256dh = subscription.P256dh;
                existing.Auth = subscription.Auth;
            }
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct)
            => await _db.PushSubscriptions.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct);

        public async Task<bool> DeleteAsync(Guid userId, string endpoint, CancellationToken ct)
            => await _db.PushSubscriptions.Where(x => x.UserId == userId && x.Endpoint == endpoint).ExecuteDeleteAsync(ct) > 0;

        public Task DeleteByEndpointAsync(string endpoint, CancellationToken ct)
            => _db.PushSubscriptions.Where(x => x.Endpoint == endpoint).ExecuteDeleteAsync(ct);
    }
}
