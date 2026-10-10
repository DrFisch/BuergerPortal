using BuergerPortal.Api.Postkorb;
using BuergerPortal.Api.Push;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Push.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Buffers.Text;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Neue Postfach-Nachricht → Benachrichtigung in der Warteschlange → Versand im Hintergrund.</summary>
    public class PushQueueTests
    {
        private sealed class FakePostkorb(PostkorbDeliveryStatus status) : IPostkorbService
        {
            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default) => Task.FromResult(status);
        }

        private sealed class OneSubscription(PushSubscription subscription) : IPushSubscriptionRepository
        {
            public Task SaveAsync(PushSubscription s, CancellationToken ct) => Task.CompletedTask;
            public Task<IReadOnlyList<PushSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<PushSubscription>>(userId == subscription.UserId ? [subscription] : []);
            public Task<bool> DeleteAsync(Guid userId, string endpoint, CancellationToken ct) => Task.FromResult(false);
            public Task DeleteByEndpointAsync(string endpoint, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class FakePushService : HttpMessageHandler
        {
            public TaskCompletionSource<HttpRequestMessage> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Received.TrySetResult(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            }
        }

        private sealed class TestController : ControllerBase;

        private static ServiceProvider Services(Action<IServiceCollection>? configure = null)
        {
            var (pub, priv) = Vapid.GenerateKeys();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Push:VapidPublicKey"] = Base64Url.EncodeToString(pub),
                ["Push:VapidPrivateKey"] = Base64Url.EncodeToString(priv),
                ["Push:Subject"] = "https://portal.example.org",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddWebPush(configuration);
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        private static TestController Controller(IServiceProvider services, Guid userId) => new()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "test")),
                    RequestServices = services,
                },
            },
        };

        [Theory]
        [InlineData(PostkorbDeliveryStatus.Delivered, true)]
        [InlineData(PostkorbDeliveryStatus.Failed, false)]
        [InlineData(PostkorbDeliveryStatus.NoMailbox, false)]
        public async Task Nur_eine_zugestellte_Nachricht_loest_eine_Benachrichtigung_aus(PostkorbDeliveryStatus status, bool expected)
        {
            using var services = Services();
            var userId = Guid.NewGuid();

            await Controller(services, userId).SendPostkorbAsync(new FakePostkorb(status), "Terminbestätigung: Reisepass",
                "Text", "Terminvereinbarung", default);

            var queued = services.GetRequiredService<PushQueue>().Reader.TryRead(out var job);
            Assert.Equal(expected, queued);
            if (expected)
            {
                Assert.Equal(userId, job!.UserId);
                Assert.Equal("Terminbestätigung: Reisepass", job.Notification.Body);   // nur der Betreff, kein Inhalt
                Assert.Equal("/Postfach", job.Notification.Url);
            }
        }

        [Fact]
        public async Task Hintergrunddienst_versendet_an_den_Push_Dienst()
        {
            var userId = Guid.NewGuid();
            using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var pushService = new FakePushService();
            using var services = Services(s =>
            {
                s.RemoveAll<IPushSubscriptionRepository>();
                s.AddSingleton<IPushSubscriptionRepository>(new OneSubscription(new PushSubscription
                {
                    UserId = userId,
                    Endpoint = "https://fcm.googleapis.com/fcm/send/abc",
                    P256dh = Base64Url.EncodeToString(WebPushEncryption.ExportPublicKey(browser)),
                    Auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
                }));
                s.AddHttpClient<WebPushSender>().ConfigurePrimaryHttpMessageHandler(() => pushService);
            });
            var worker = services.GetServices<IHostedService>().OfType<PushQueueWorker>().Single();
            await worker.StartAsync(default);

            Assert.True(services.GetRequiredService<PushQueue>().TryEnqueue(userId,
                new PushNotification("Neue Nachricht im BundID-Postfach", "Test", "/Postfach", "postfach")));
            var request = await pushService.Received.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("https://fcm.googleapis.com/fcm/send/abc", request.RequestUri!.ToString());
            await worker.StopAsync(default);
        }
    }
}
