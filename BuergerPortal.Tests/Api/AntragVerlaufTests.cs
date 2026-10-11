using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Zeitleiste über die API: nur eigene Anträge, ohne Anmeldung nichts (echter API-Host, InMemory-Datenbank).</summary>
    public class AntragVerlaufTests
    {
        private const string Person = "11111111-2222-3333-4444-555555555555";

        private static async Task<Guid> SeedAsync(ApiTestFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var antrag = new SperrmuellAntrag
            {
                Id = Guid.NewGuid(),
                ApplicantUserId = Guid.Parse(Person),
                Name = new PersonName("Tina", "Test"),
                Birth = new PersonBirth(new DateOnly(1990, 1, 1)),
                Kontakt = new Kontakt(null, null),
                Mengen = new SperrmuellMengen(1, null, null),
                Addresse = new Addresse("Hauptstraße 1", "90402", "Nürnberg"),
                Wunschzeit = new DateTime(2026, 10, 20),
                RowVersion = [1],
            };
            db.SperrmuellAntraege.Add(antrag);
            await db.SaveChangesAsync();
            var repo = scope.ServiceProvider.GetRequiredService<IAntragStatusRepository>();
            await repo.SetStatusAsync(antrag.Id, AntragStatus.Eingereicht, new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc), "h", CancellationToken.None);
            await repo.SetStatusAsync(antrag.Id, AntragStatus.InPruefung, new DateTime(2026, 10, 11, 8, 2, 0, DateTimeKind.Utc), null, CancellationToken.None);
            return antrag.Id;
        }

        [Fact]
        public async Task Eigene_Zeitleiste_in_Reihenfolge()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory);

            var response = await factory.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/antraege/{id}/verlauf").As(Person));
            var json = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(json.IndexOf("\"status\":2", StringComparison.Ordinal) < json.IndexOf("\"status\":3", StringComparison.Ordinal), json);
            Assert.Contains("2026-10-11T08:02:00Z", json);
        }

        [Fact]
        public async Task Fremder_Antrag_404_ohne_Anmeldung_401()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory);
            var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, $"/api/antraege/{id}/verlauf").As(Guid.NewGuid().ToString()))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/antraege/{id}/verlauf")).StatusCode);
        }
    }
}
