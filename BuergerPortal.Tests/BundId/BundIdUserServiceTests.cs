using AuthenticationServer.BundId;
using BuergerPortal.BundId;
using AuthenticationServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>Just-in-Time-Anlage und Wiedererkennung von Konten über die bPK2.</summary>
    public class BundIdUserServiceTests
    {
        private static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var database = Guid.NewGuid().ToString();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(database));
            services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<BundIdUserService>();
            return services.BuildServiceProvider();
        }

        private static BundIdAttributes Person(string bpk2, int level = TrustLevel.Normal, string? handle = null) => new()
        {
            Bpk2 = bpk2,
            GivenName = "Tina",
            FamilyName = "Test",
            PostkorbHandle = handle,
            TrustLevel = level,
        };

        [Fact]
        public async Task Erster_Login_legt_Konto_ohne_Passwort_an()
        {
            using var scope = CreateServices().CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BundIdUserService>();

            var (user, created) = await service.FindOrCreateAsync(Person("BUNDIDSIM-T01"));

            Assert.True(created);
            Assert.Equal("BUNDIDSIM-T01", user.Bpk2);
            Assert.StartsWith("bundid-", user.UserName);
            Assert.Null(user.PasswordHash);
            Assert.NotNull(user.CreatedViaBundIdUtc);
        }

        [Fact]
        public async Task Dieselbe_bPK2_wird_wiedererkannt()
        {
            var services = CreateServices();
            string firstId;
            using (var scope = services.CreateScope())
            {
                var (user, _) = await scope.ServiceProvider.GetRequiredService<BundIdUserService>()
                    .FindOrCreateAsync(Person("BUNDIDSIM-T02"));
                firstId = user.Id;
            }

            using (var scope = services.CreateScope())
            {
                var (user, created) = await scope.ServiceProvider.GetRequiredService<BundIdUserService>()
                    .FindOrCreateAsync(Person("BUNDIDSIM-T02"));
                Assert.False(created);
                Assert.Equal(firstId, user.Id);
            }
        }

        [Fact]
        public async Task Verschiedene_Personen_bekommen_eigene_Konten()
        {
            using var scope = CreateServices().CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BundIdUserService>();

            var (first, _) = await service.FindOrCreateAsync(Person("BUNDIDSIM-U02-bp"));
            var (second, _) = await service.FindOrCreateAsync(Person("BUNDIDSIM-U02-anders"));

            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public async Task Login_speichert_Postkorb_Handle_Niveau_und_Zeitpunkt()
        {
            using var scope = CreateServices().CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BundIdUserService>();
            var attributes = Person("BUNDIDSIM-T03", TrustLevel.Substantial, "11b2dc8f-3831-3b26-afde-aa0be42bd79b");
            var (user, _) = await service.FindOrCreateAsync(attributes);

            await service.UpdateLoginDataAsync(user, attributes);

            var stored = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id);
            Assert.Equal("11b2dc8f-3831-3b26-afde-aa0be42bd79b", stored!.PostkorbHandle);
            Assert.Equal(TrustLevel.Substantial, stored.TrustLevel);
            Assert.NotNull(stored.LastLoginUtc);
        }

        [Fact]
        public async Task Jede_Anmeldung_wird_protokolliert_nur_die_letzten_fuenf_bleiben()
        {
            using var scope = CreateServices().CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BundIdUserService>();
            var (user, _) = await service.FindOrCreateAsync(Person("BUNDIDSIM-T09"));

            for (var i = 0; i < 7; i++)
            {
                await service.UpdateLoginDataAsync(user, Person("BUNDIDSIM-T09", i % 2 == 0 ? TrustLevel.Normal : TrustLevel.Substantial)
                    with { IdentificationMethod = i % 2 == 0 ? "Benutzername" : "Elster" });
            }

            var letzte = await service.GetLetzteAnmeldungenAsync(user.Id);
            Assert.Equal(BundIdUserService.AnzahlLetzteAnmeldungen, letzte.Count);
            Assert.Equal((TrustLevel.Normal, "Benutzername"), (letzte[0].Niveau, letzte[0].Mittel));   // neueste zuerst (i = 6)
            Assert.Equal(5, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().BundIdAnmeldungen.CountAsync());
        }
    }
}
