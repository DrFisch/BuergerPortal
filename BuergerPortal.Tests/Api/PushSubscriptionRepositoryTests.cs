using BuergerPortal.Domain.Push.Entity;
using BuergerPortal.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Push-Abos gegen einen echten SQL Server (siehe SqlServerTest): Die Migrationen werden auf die Test-DB angewendet,
    /// jeder Test nutzt eigene Personen und Adressen und räumt danach auf.
    /// </summary>
    public class PushSubscriptionRepositoryTests
    {
        private static async Task<PushSubscriptionRepository> NewRepositoryAsync()
        {
            var db = SqlServerTest.NewContext();
            await db.Database.MigrateAsync();
            return new PushSubscriptionRepository(db);
        }

        private static PushSubscription Abo(Guid userId, string endpoint, string p256dh = "BKey") =>
            new() { UserId = userId, Endpoint = endpoint, P256dh = p256dh, Auth = "auth" };

        [SqlServerFact]
        public async Task Gleiches_Geraet_gehoert_nach_Anmeldung_einer_anderen_Person_dieser()
        {
            var repo = await NewRepositoryAsync();
            var (anna, ben) = (Guid.NewGuid(), Guid.NewGuid());
            var endpoint = $"https://push.example.net/test/{Guid.NewGuid()}";
            try
            {
                await repo.SaveAsync(Abo(anna, endpoint), default);
                await repo.SaveAsync(Abo(ben, endpoint, "BNeu"), default);

                Assert.Empty(await repo.GetByUserIdAsync(anna, default));
                var abo = Assert.Single(await repo.GetByUserIdAsync(ben, default));
                Assert.Equal("BNeu", abo.P256dh);
            }
            finally
            {
                await repo.DeleteByEndpointAsync(endpoint, default);
            }
        }

        [SqlServerFact]
        public async Task Nur_eigene_Abos_lassen_sich_entfernen()
        {
            var repo = await NewRepositoryAsync();
            var (anna, ben) = (Guid.NewGuid(), Guid.NewGuid());
            var endpoint = $"https://push.example.net/test/{Guid.NewGuid()}";
            try
            {
                await repo.SaveAsync(Abo(anna, endpoint), default);

                Assert.False(await repo.DeleteAsync(ben, endpoint, default));
                Assert.Single(await repo.GetByUserIdAsync(anna, default));
                Assert.True(await repo.DeleteAsync(anna, endpoint, default));
                Assert.Empty(await repo.GetByUserIdAsync(anna, default));
            }
            finally
            {
                await repo.DeleteByEndpointAsync(endpoint, default);
            }
        }
    }
}
