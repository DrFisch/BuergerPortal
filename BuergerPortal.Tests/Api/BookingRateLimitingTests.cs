using BuergerPortal.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Buchungen je Person begrenzen: nach der Grenze 429 mit verständlicher Meldung, andere Person nicht betroffen,
    /// Lesen nicht begrenzt (echter API-Host, InMemory-Datenbank, Bearer-Anmeldung durch ein Test-Schema ersetzt).
    /// </summary>
    public class BookingRateLimitingTests
    {
        private static readonly string PersonA = Guid.NewGuid().ToString();   // sub ist im Portal eine GUID

        // Inhalt egal (ungültig → 400): Die Grenze greift vor der Prüfung des Inhalts.
        private static HttpRequestMessage Book(string sub) =>
            new HttpRequestMessage(HttpMethod.Post, "/api/appointments") { Content = JsonContent.Create(new { }) }.As(sub);

        [Fact]
        public async Task Nach_zwei_Buchungen_je_Minute_folgt_429_mit_Meldung()
        {
            using var factory = new ApiTestFactory(new Dictionary<string, string?> { ["RateLimiting:BuchungenProMinute"] = "2" });
            using var client = factory.CreateClient();

            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(PersonA))).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(PersonA))).StatusCode);
            var rejected = await client.SendAsync(Book(PersonA));

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("60", rejected.Headers.GetValues("Retry-After").Single());
            var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(BookingRateLimiting.RejectedDetail, problem!.Detail);

            // eine andere Person ist nicht betroffen, Lesen ist nicht begrenzt
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(Guid.NewGuid().ToString()))).StatusCode);
            var read = new HttpRequestMessage(HttpMethod.Get, "/api/appointments/mine").As(PersonA);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(read)).StatusCode);
        }
    }
}
