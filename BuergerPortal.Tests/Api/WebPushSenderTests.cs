using BuergerPortal.Api.Push;
using BuergerPortal.Domain.Push.Entity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Net;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Versand an den Push-Dienst (ohne Netz, mit nachgebildetem Push-Dienst) und Prüfung der erlaubten Adressen.</summary>
    public class WebPushSenderTests
    {
        private sealed class FakePushService(HttpStatusCode status) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];
            public byte[] LastBody { get; private set; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Requests.Add(request);
                LastBody = await request.Content!.ReadAsByteArrayAsync(ct);
                return new HttpResponseMessage(status);
            }
        }

        private static PushOptions Configured()
        {
            var (publicKey, privateKey) = Vapid.GenerateKeys();
            return new PushOptions
            {
                VapidPublicKey = Base64Url.EncodeToString(publicKey),
                VapidPrivateKey = Base64Url.EncodeToString(privateKey),
                Subject = "https://portal.example.org",
            };
        }

        private static PushSubscription Abo(string endpoint)
        {
            using var browser = System.Security.Cryptography.ECDiffieHellman.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            return new PushSubscription
            {
                Endpoint = endpoint,
                P256dh = Base64Url.EncodeToString(WebPushEncryption.ExportPublicKey(browser)),
                Auth = Base64Url.EncodeToString(new byte[16]),
            };
        }

        private static (WebPushSender Sender, FakePushService Service) Create(HttpStatusCode status, PushOptions? options = null)
        {
            var service = new FakePushService(status);
            var sender = new WebPushSender(new HttpClient(service), Options.Create(options ?? Configured()), TimeProvider.System,
                NullLogger<WebPushSender>.Instance);
            return (sender, service);
        }

        private static readonly PushNotification Nachricht = new("Neue Nachricht", "Terminbestätigung", "/Postfach", "postfach");

        [Fact]
        public async Task Sendet_verschluesselt_mit_VAPID_und_Lebensdauer()
        {
            var (sender, service) = Create(HttpStatusCode.Created);

            var result = await sender.SendAsync(Abo("https://fcm.googleapis.com/fcm/send/abc"), Nachricht, default);

            Assert.Equal(PushSendResult.Sent, result);
            var request = Assert.Single(service.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("aes128gcm", Assert.Single(request.Content!.Headers.ContentEncoding));
            Assert.Equal("86400", request.Headers.GetValues("TTL").Single());
            Assert.StartsWith("vapid t=", request.Headers.GetValues("Authorization").Single());
            Assert.Equal(4096u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(service.LastBody.AsSpan(16, 4)));
        }

        [Theory]
        [InlineData(HttpStatusCode.Gone, PushSendResult.Gone)]
        [InlineData(HttpStatusCode.NotFound, PushSendResult.Gone)]
        [InlineData(HttpStatusCode.Forbidden, PushSendResult.Failed)]
        public async Task Antwort_des_Push_Dienstes_wird_ausgewertet(HttpStatusCode status, PushSendResult expected)
        {
            var (sender, _) = Create(status);

            Assert.Equal(expected, await sender.SendAsync(Abo("https://updates.push.services.mozilla.com/wpush/v2/x"), Nachricht, default));
        }

        [Fact]
        public async Task Fremde_Adresse_wird_nicht_angefragt()
        {
            var (sender, service) = Create(HttpStatusCode.Created);

            var result = await sender.SendAsync(Abo("https://bpsim-sql:1433/intern"), Nachricht, default);

            Assert.Equal(PushSendResult.Failed, result);
            Assert.Empty(service.Requests);
        }

        [Fact]
        public async Task Ohne_Schluessel_ist_Push_aus()
        {
            var (sender, service) = Create(HttpStatusCode.Created, new PushOptions());

            Assert.Equal(PushSendResult.Failed, await sender.SendAsync(Abo("https://fcm.googleapis.com/fcm/send/abc"), Nachricht, default));
            Assert.Empty(service.Requests);
        }

        [Theory]
        [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
        [InlineData("https://wns2-par02p.notify.windows.com/w/?token=x", true)]
        [InlineData("https://web.push.apple.com/QGx", true)]
        [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x", true)]
        [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]          // kein https
        [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]    // anderer Port
        [InlineData("https://fcm.googleapis.com.example.net/x", false)]        // nur ähnlich
        [InlineData("https://notify.windows.com.example.net/x", false)]
        [InlineData("https://169.254.169.254/computeMetadata/v1/", false)]     // IP-Adresse (Metadaten der VM)
        [InlineData("https://localhost/x", false)]
        public void Nur_bekannte_Push_Dienste_sind_erlaubt(string endpoint, bool allowed)
        {
            Assert.Equal(allowed, new PushOptions().IsAllowedEndpoint(new Uri(endpoint)));
        }
    }
}
