using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Infrastructure.Postkorb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace BuergerPortal.Tests.Postkorb
{
    /// <summary>Postkorb-Client der API: Aufbau der Nachricht und Verhalten bei Fehlern (ohne Netzwerk).</summary>
    public class HttpPostkorbServiceTests
    {
        private const string Handle = "11b2dc8f-3831-3b26-afde-aa0be42bd79b";
        private const string Key = "test-schluessel-mit-mindestens-32-zeichen";

        /// <summary>Ersetzt das Netzwerk: merkt sich die Anfragen und antwortet wie vorgegeben.</summary>
        private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
                Requests.Add((request, body));
                return respond(request);
            }
        }

        // Registrierung wie in der API (AddPostkorbService), nur mit Fake-Handler statt echtem Netzwerk.
        private static (IPostkorbService Service, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            var handler = new FakeHandler(respond);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postkorb:BaseUrl"] = "https://postkorb.example.test",
                ["Postkorb:ApiKey"] = Key,
                ["Postkorb:Sender"] = "Testbehörde",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPostkorbService(config);
            services.AddHttpClient<IPostkorbService, HttpPostkorbService>().ConfigurePrimaryHttpMessageHandler(() => handler);
            return (services.BuildServiceProvider().GetRequiredService<IPostkorbService>(), handler);
        }

        private static PostkorbMessage Message(string? handle = Handle) =>
            new(handle, "Terminbestätigung", "Ihr Termin ist gebucht.", "Terminvereinbarung", StorkQaaLevel: 3);

        [Fact]
        public async Task Zustellung_sendet_CreateMessage_mit_API_Schluessel()
        {
            var (service, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.Created));

            var status = await service.SendAsync(Message());

            Assert.Equal(PostkorbDeliveryStatus.Delivered, status);
            var (request, body) = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://postkorb.example.test/api/v1/messages", request.RequestUri!.ToString());
            Assert.Equal(Key, Assert.Single(request.Headers.GetValues(HttpPostkorbService.ApiKeyHeader)));
            var json = JsonDocument.Parse(body!).RootElement;
            Assert.Equal(Handle, json.GetProperty("mailboxUuid").GetString());
            Assert.Equal("Terminbestätigung", json.GetProperty("title").GetString());
            Assert.Equal("Testbehörde", json.GetProperty("sender").GetString());
            Assert.Equal("Terminvereinbarung", json.GetProperty("service").GetString());
            Assert.Equal(3, json.GetProperty("storkQaaLevel").GetInt32());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("kein-uuid")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public async Task Ohne_Postkorb_Handle_wird_nichts_gesendet(string? handle)
        {
            var (service, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.Created));

            Assert.Equal(PostkorbDeliveryStatus.NoMailbox, await service.SendAsync(Message(handle)));
            Assert.Empty(handler.Requests);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Abgelehnte_Nachricht_meldet_Failed(HttpStatusCode code)
        {
            var (service, _) = Create(_ => new HttpResponseMessage(code));

            Assert.Equal(PostkorbDeliveryStatus.Failed, await service.SendAsync(Message()));
        }

        [Fact]
        public async Task Nicht_erreichbarer_Postkorb_meldet_Failed_statt_Ausnahme()
        {
            var (service, _) = Create(_ => throw new HttpRequestException("Verbindung abgelehnt"));

            Assert.Equal(PostkorbDeliveryStatus.Failed, await service.SendAsync(Message()));
        }

        [Fact]
        public async Task Timeout_meldet_Failed_statt_Ausnahme()
        {
            var (service, _) = Create(_ => throw new TaskCanceledException("Timeout"));

            Assert.Equal(PostkorbDeliveryStatus.Failed, await service.SendAsync(Message()));
        }

        [Fact]
        public void Konfiguration_ohne_Schluessel_wird_abgelehnt()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postkorb:BaseUrl"] = "https://postkorb.example.test",
            }).Build();
            var services = new ServiceCollection();
            services.AddPostkorbService(config);

            var ex = Assert.Throws<OptionsValidationException>(() =>
                services.BuildServiceProvider().GetRequiredService<IOptions<PostkorbOptions>>().Value);
            Assert.Contains("ApiKey", ex.Message);
        }
    }
}
