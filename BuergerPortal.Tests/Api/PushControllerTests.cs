using BuergerPortal.Api.Push;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Push.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// API der Push-Abos über den echten API-Host: Schlüssel, Prüfung der Abos, nur eigene Abos entfernen,
    /// Test-Benachrichtigung (nachgebildeter Push-Dienst), eigene Grenze je Minute.
    /// </summary>
    public class PushControllerTests
    {
        private sealed class MemoryRepository : IPushSubscriptionRepository
        {
            public List<PushSubscription> Items { get; } = [];

            public Task SaveAsync(PushSubscription s, CancellationToken ct)
            {
                Items.RemoveAll(x => x.Endpoint == s.Endpoint);
                Items.Add(s);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<PushSubscription>>(Items.Where(x => x.UserId == userId).ToList());

            public Task<bool> DeleteAsync(Guid userId, string endpoint, CancellationToken ct) =>
                Task.FromResult(Items.RemoveAll(x => x.UserId == userId && x.Endpoint == endpoint) > 0);

            public Task DeleteByEndpointAsync(string endpoint, CancellationToken ct)
            {
                Items.RemoveAll(x => x.Endpoint == endpoint);
                return Task.CompletedTask;
            }
        }

        private sealed class FakePushService : HttpMessageHandler
        {
            public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
            public int Requests { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Requests++;
                return Task.FromResult(new HttpResponseMessage(Status));
            }
        }

        private readonly MemoryRepository repository = new();
        private readonly FakePushService pushService = new();
        private readonly string anna = Guid.NewGuid().ToString();
        private readonly string ben = Guid.NewGuid().ToString();

        private ApiTestFactory Factory(bool configured = true)
        {
            var (pub, priv) = Vapid.GenerateKeys();
            var settings = configured
                ? new Dictionary<string, string?>
                {
                    ["Push:VapidPublicKey"] = Base64Url.EncodeToString(pub),
                    ["Push:VapidPrivateKey"] = Base64Url.EncodeToString(priv),
                    ["Push:Subject"] = "https://portal.example.org",
                }
                : null;
            return new ApiTestFactory(settings, s =>
            {
                s.RemoveAll<IPushSubscriptionRepository>();
                s.AddSingleton<IPushSubscriptionRepository>(repository);
                s.AddHttpClient<WebPushSender>().ConfigurePrimaryHttpMessageHandler(() => pushService);
            });
        }

        private static object Abo(string endpoint, string? p256dh = null, string? auth = null)
        {
            using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            return new
            {
                endpoint,
                expirationTime = (object?)null,
                keys = new
                {
                    p256dh = p256dh ?? Base64Url.EncodeToString(WebPushEncryption.ExportPublicKey(browser)),
                    auth = auth ?? Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
                },
            };
        }

        private static HttpRequestMessage Subscribe(string sub, object abo) =>
            new HttpRequestMessage(HttpMethod.Post, "/api/push/subscriptions") { Content = JsonContent.Create(abo) }.As(sub);

        [Fact]
        public async Task Schluessel_nur_wenn_Push_eingerichtet_ist()
        {
            using (var off = Factory(configured: false))
            {
                Assert.Equal(HttpStatusCode.NotFound, (await off.CreateClient().GetAsync("/api/push/key")).StatusCode);
            }
            using var on = Factory();
            var key = await on.CreateClient().GetFromJsonAsync<Dictionary<string, string>>("/api/push/key");
            Assert.Equal(87, key!["publicKey"].Length);   // 65 Byte base64url
        }

        [Fact]
        public async Task Abo_wird_fuer_die_angemeldete_Person_gespeichert()
        {
            using var factory = Factory();
            using var client = factory.CreateClient();

            var response = await client.SendAsync(Subscribe(anna, Abo("https://fcm.googleapis.com/fcm/send/anna-1")));

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var abo = Assert.Single(repository.Items);
            Assert.Equal(Guid.Parse(anna), abo.UserId);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/push/subscriptions",
                Abo("https://fcm.googleapis.com/fcm/send/x"))).StatusCode);
        }

        [Theory]
        [InlineData("https://bpsim-api:8080/api/appointments", null, null)]               // interne Adresse
        [InlineData("https://fcm.googleapis.com/fcm/send/x", "BBBB", null)]               // Schlüssel zu kurz
        [InlineData("https://fcm.googleapis.com/fcm/send/x", null, "AAAA")]               // Auth-Geheimnis zu kurz
        [InlineData("https://fcm.googleapis.com/fcm/send/x",
            "BAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", null)]  // kein Kurvenpunkt
        public async Task Ungueltige_Abos_werden_abgelehnt(string endpoint, string? p256dh, string? auth)
        {
            using var factory = Factory();

            var response = await factory.CreateClient().SendAsync(Subscribe(anna, Abo(endpoint, p256dh, auth)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(repository.Items);
        }

        [Fact]
        public async Task Nur_eigene_Abos_lassen_sich_entfernen()
        {
            using var factory = Factory();
            using var client = factory.CreateClient();
            const string endpoint = "https://fcm.googleapis.com/fcm/send/anna-2";
            await client.SendAsync(Subscribe(anna, Abo(endpoint)));
            var url = "/api/push/subscriptions?endpoint=" + Uri.EscapeDataString(endpoint);

            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url).As(ben))).StatusCode);
            Assert.Single(repository.Items);
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url).As(anna))).StatusCode);
            Assert.Empty(repository.Items);
        }

        [Fact]
        public async Task Test_Benachrichtigung_geht_an_die_eigenen_Geraete_verwaiste_Abos_werden_geloescht()
        {
            using var factory = Factory();
            using var client = factory.CreateClient();
            await client.SendAsync(Subscribe(anna, Abo("https://fcm.googleapis.com/fcm/send/anna-3")));
            await client.SendAsync(Subscribe(ben, Abo("https://fcm.googleapis.com/fcm/send/ben-1")));

            var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/push/test").As(anna));
            Assert.Equal(1, (await first.Content.ReadFromJsonAsync<Dictionary<string, int>>())!["sent"]);
            Assert.Equal(1, pushService.Requests);   // Bens Gerät nicht

            pushService.Status = HttpStatusCode.Gone;   // z. B. Berechtigung im Browser entzogen
            var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/push/test").As(anna));
            Assert.Equal(0, (await second.Content.ReadFromJsonAsync<Dictionary<string, int>>())!["sent"]);
            Assert.DoesNotContain(repository.Items, x => x.UserId == Guid.Parse(anna));
            Assert.Contains(repository.Items, x => x.UserId == Guid.Parse(ben));
        }

        [Fact]
        public async Task Nach_zehn_Anfragen_je_Minute_folgt_429_mit_eigener_Meldung()
        {
            using var factory = Factory();
            using var client = factory.CreateClient();
            for (var i = 0; i < PushRateLimitPolicy.PermitLimit; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/push/test").As(anna))).StatusCode);
            }

            var rejected = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/push/test").As(anna));

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal(PushRateLimitPolicy.RejectedDetail, (await rejected.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        }
    }
}
