using BuergerPortal.Domain.Maengel;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using BuergerPortal.Api.Contracts.Maengelmeldungen;
using System.Net;
using System.Net.Http.Json;

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

        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        private static HttpRequestMessage Melden(string? fotoBase64, string? typ) =>
            new HttpRequestMessage(HttpMethod.Post, "/api/maengelmeldungen")
            {
                Content = JsonContent.Create(new
                {
                    titel = "Laterne defekt", beschreibung = "Seit Tagen dunkel", latitude = 49.45, longitude = 11.08,
                    fotoBase64, fotoContentType = typ,
                }),
            }.As(Person);

        [Fact]
        public async Task Foto_wird_gespeichert_und_nur_der_meldenden_Person_gezeigt()
        {
            using var factory = new ApiTestFactory();
            var client = factory.CreateClient();

            var created = await client.SendAsync(Melden(Convert.ToBase64String(Jpeg), "image/jpeg"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var meine = await (await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/maengelmeldungen/mine").As(Person)))
                .Content.ReadFromJsonAsync<List<MaengelmeldungResponse>>();
            var meldung = Assert.Single(meine!);
            Assert.True(meldung.HatFoto);

            var foto = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/maengelmeldungen/{meldung.Id}/foto").As(Person));
            Assert.Equal("image/jpeg", foto.Content.Headers.ContentType!.MediaType);
            Assert.Equal(Jpeg, await foto.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, $"/api/maengelmeldungen/{meldung.Id}/foto").As(Guid.NewGuid().ToString()))).StatusCode);
        }

        [Fact]
        public async Task Getarnte_Datei_wird_abgelehnt()
        {
            using var factory = new ApiTestFactory();
            var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Melden(Convert.ToBase64String(Jpeg), "image/png"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(
                Melden(Convert.ToBase64String("<script>alert(1)</script>"u8.ToArray()), "image/jpeg"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Melden(Convert.ToBase64String(Jpeg), "image/svg+xml"))).StatusCode);
        }

        [Fact]
        public void Zu_grosses_Foto_wird_abgelehnt()
        {
            var gross = new byte[MangelFotoPruefung.MaxBytes + 1];
            Jpeg.CopyTo(gross, 0);

            Assert.False(MangelFotoPruefung.TryDecode(Convert.ToBase64String(gross), "image/jpeg", out _, out var fehler));
            Assert.Contains("2 MB", fehler);
        }
    }
}
