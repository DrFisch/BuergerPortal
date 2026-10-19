using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.Mappings;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Meldebescheinigung (echter API-Host): Entwurf mit jedem Niveau, einreichen erst ab „hoch“ (STORK-QAA-Level-4),
    /// Eingangsbestätigung ins Postfach mit Niveau 4, nur eigene Anträge.
    /// </summary>
    public class MeldebescheinigungApiTests
    {
        private static readonly string Person = Guid.NewGuid().ToString();

        private sealed class FakePostkorb : IPostkorbService
        {
            public List<PostkorbMessage> Sent { get; } = [];

            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default)
            {
                Sent.Add(message);
                return Task.FromResult(PostkorbDeliveryStatus.Delivered);
            }
        }

        private static object Angaben(string strasse = "Rankestr. 13", int art = 2) => new
        {
            vorname = "Manfred",
            nachname = "Schmidt",
            geburtsdatum = "1969-02-11",
            geburtsort = "Nürnberg",
            geburtsname = (string?)null,
            strasse,
            plz = "90461",
            ort = "Nürnberg",
            email = "m.schmidt@example.com",
            art,
            zweck = "für den Arbeitgeber",
        };

        private static HttpRequestMessage Anfrage(HttpMethod methode, string pfad, string person, int niveau,
            object? inhalt = null)
        {
            var request = new HttpRequestMessage(methode, pfad).As(person, niveau);
            if (inhalt is not null) request.Content = JsonContent.Create(inhalt);
            return request;
        }

        // Entwurf direkt in der Datenbank: die InMemory-Datenbank erzeugt keine RowVersion (wie AntragStatusRepositoryTests)
        private static async Task<Guid> AnlegenAsync(ApiTestFactory factory)
        {
            var antrag = MeldebescheinigungMapper.ToNewEntity(new MeldebescheinigungDto("Manfred", "Schmidt",
                new DateOnly(1969, 2, 11), "Nürnberg", null, "Rankestr. 13", "90461", "Nürnberg", null,
                MeldebescheinigungArt.Erweitert, null), Guid.Parse(Person));
            antrag.RowVersion = [1];
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            db.MeldebescheinigungAntraege.Add(antrag);
            await db.SaveChangesAsync();
            return antrag.Id;
        }

        private sealed record IdAntwort(Guid Id);

        [Fact]
        public async Task Einreichen_erst_ab_Niveau_hoch_mit_Bestaetigung_im_Postfach()
        {
            var postkorb = new FakePostkorb();
            using var factory = new ApiTestFactory(services: s =>
            {
                s.RemoveAll<IPostkorbService>();
                s.AddSingleton<IPostkorbService>(postkorb);
            });
            var client = factory.CreateClient();
            var id = await AnlegenAsync(factory);
            var submit = $"/api/antraege/meldebescheinigung/{id}/submit";

            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Anfrage(HttpMethod.Post, submit, Person, 1))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Anfrage(HttpMethod.Post, submit, Person, 3))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Anfrage(HttpMethod.Post, submit, Person, 4))).StatusCode);

            using var scope = factory.Services.CreateScope();
            var antrag = await scope.ServiceProvider.GetRequiredService<PortalDbContext>().MeldebescheinigungAntraege.SingleAsync(a => a.Id == id);
            Assert.Equal(AntragStatus.Eingereicht, antrag.Status);
            Assert.Equal(MeldebescheinigungArt.Erweitert, antrag.Art);
            var nachricht = Assert.Single(postkorb.Sent);
            Assert.Equal(4, nachricht.StorkQaaLevel);
            Assert.Contains("erweiterte Meldebescheinigung", nachricht.Content);
        }

        [Fact]
        public async Task Entwurf_aendern_ohne_Anschrift_400_nach_Einreichen_nicht_mehr()
        {
            using var factory = new ApiTestFactory();
            var client = factory.CreateClient();
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Anfrage(HttpMethod.Post,
                "/api/antraege/meldebescheinigung", Person, 1, Angaben(strasse: "")))).StatusCode);
            var id = await AnlegenAsync(factory);
            var pfad = $"/api/antraege/meldebescheinigung/{id}";

            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Anfrage(HttpMethod.Put, pfad, Person, 1, Angaben(strasse: "")))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Anfrage(HttpMethod.Put, pfad, Person, 1, Angaben(art: 1)))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Anfrage(HttpMethod.Post, pfad + "/submit", Person, 4))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Anfrage(HttpMethod.Put, pfad, Person, 4, Angaben()))).StatusCode);
        }

        [Fact]
        public async Task Fremder_Antrag_403_Liste_nur_eigene()
        {
            using var factory = new ApiTestFactory();
            var client = factory.CreateClient();
            var id = await AnlegenAsync(factory);
            var fremd = Guid.NewGuid().ToString();

            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.SendAsync(Anfrage(HttpMethod.Get, $"/api/antraege/meldebescheinigung/{id}", fremd, 4))).StatusCode);
            var liste = await (await client.SendAsync(Anfrage(HttpMethod.Get, "/api/antraege/meldebescheinigung", fremd, 4)))
                .Content.ReadFromJsonAsync<List<IdAntwort>>();
            Assert.Empty(liste!);
            var eigene = await (await client.SendAsync(Anfrage(HttpMethod.Get, "/api/antraege/meldebescheinigung", Person, 1)))
                .Content.ReadFromJsonAsync<List<IdAntwort>>();
            Assert.Equal(id, Assert.Single(eigene!).Id);
        }
    }
}
