using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Infrastructure.Persistence;
using BuergerPortal.Tests.Antraege;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Entwurf verwerfen (DELETE api/antraege/{id}): nur eigene Entwürfe, eingereichte bleiben (echter API-Host).</summary>
    public class AntragEntwurfTests
    {
        private static readonly string Person = AntragStatusRepositoryTests.Person.ToString();

        private static async Task<Guid> SeedAsync(ApiTestFactory factory, AntragStatus status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            return (await AntragStatusRepositoryTests.SeedAsync(db, status)).Id;
        }

        private static HttpRequestMessage Loeschen(Guid id, string? person = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/antraege/{id}");
            return person is null ? request : request.As(person);
        }

        [Fact]
        public async Task Eigener_Entwurf_wird_verworfen_und_ist_danach_weg()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory, AntragStatus.Entwurf);
            var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Loeschen(id, Person))).StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            Assert.False(await db.Antraege.AnyAsync(a => a.Id == id));
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Loeschen(id, Person))).StatusCode);
        }

        [Fact]
        public async Task Eingereichter_Antrag_bleibt_409()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory, AntragStatus.Eingereicht);

            var response = await factory.CreateClient().SendAsync(Loeschen(id, Person));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<PortalDbContext>().Antraege.AnyAsync(a => a.Id == id));
        }

        [Fact]
        public async Task Fremder_Entwurf_404_ohne_Anmeldung_401()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory, AntragStatus.Entwurf);
            var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Loeschen(id, Guid.NewGuid().ToString()))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Loeschen(id))).StatusCode);

            using var scope = factory.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<PortalDbContext>().Antraege.AnyAsync(a => a.Id == id));
        }
    }
}
