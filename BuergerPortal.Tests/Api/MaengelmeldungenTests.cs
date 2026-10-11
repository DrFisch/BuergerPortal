using BuergerPortal.Domain.Maengel;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Mängelmeldungen über die API: nur die eigene Meldung, ohne Anmeldung nichts (echter API-Host, InMemory).</summary>
    public class MaengelmeldungenTests
    {
        internal const string Person = "11111111-2222-3333-4444-555555555555";

        internal static async Task<Guid> SeedAsync(ApiTestFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var meldung = new Maengelmeldung
            {
                Id = Guid.NewGuid(), ReporterUserId = Guid.Parse(Person), Titel = "Schlagloch", Beschreibung = "Tief",
                Latitude = 49.45, Longitude = 11.08,
            };
            db.Maengelmeldungen.Add(meldung);
            await db.SaveChangesAsync();
            return meldung.Id;
        }

        [Fact]
        public async Task Eigene_Meldung_ohne_Melder_ID_fremde_404_ohne_Anmeldung_401()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory);
            var client = factory.CreateClient();

            var eigene = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/maengelmeldungen/{id}").As(Person));
            var json = await eigene.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, eigene.StatusCode);
            Assert.Contains("Schlagloch", json);
            Assert.DoesNotContain(Person, json);
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, $"/api/maengelmeldungen/{id}").As(Guid.NewGuid().ToString()))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/maengelmeldungen/{id}")).StatusCode);
        }
    }
}
