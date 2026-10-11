using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Mängelmelder im Portal: Foto geht als Base64 mit Typ an die API, zu große Fotos nicht; „Meine Meldungen“ mit Foto
    /// (echter Host des Portals, Anmeldung durch ein Test-Schema ersetzt, API nachgebildet).
    /// </summary>
    public class MangelFotoTests
    {
        private static readonly Guid Meldung = Guid.Parse("8b000000-0000-4000-8000-000000000001");
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        private static ClaimsPrincipal Person() => new(new ClaimsIdentity(
            [new Claim("sub", "test-person"), new Claim("name", "Test Person")], TestAuthHandler.SchemeName, "name", "role"));

        private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
                Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Person(), SchemeName)));
        }

        private sealed class FakeApi : HttpMessageHandler
        {
            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var path = request.RequestUri!.AbsolutePath;
                if (request.Method == HttpMethod.Post && path == "/api/maengelmeldungen")
                {
                    Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = JsonContent.Create(new { id = Meldung, createdUtc = DateTime.UtcNow, status = "Offen" }),
                    };
                }
                if (path == "/api/maengelmeldungen/mine")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new[]
                        {
                            new { id = Meldung, createdUtc = "2026-10-11T08:00:00Z", titel = "Laterne defekt", beschreibung = "dunkel",
                                  latitude = 49.45, longitude = 11.08, addressHint = (string?)null, status = "Offen", hatFoto = true },
                        }),
                    };
                }
                if (path == $"/api/maengelmeldungen/{Meldung}/foto")
                {
                    var content = new ByteArrayContent(Jpeg);
                    content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private sealed class PortalFactory(FakeApi api) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                }));
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    services.AddHttpClient("BuergerPortalApi").ConfigurePrimaryHttpMessageHandler(() => api);
                });
            }
        }

        private static HttpRequestMessage Melden(PortalFactory factory, byte[] foto)
        {
            var antiforgery = factory.Services.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetTokens(new DefaultHttpContext { RequestServices = factory.Services, User = Person(), Request = { Scheme = "https" } });
            var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
            var form = new MultipartFormDataContent
            {
                { new StringContent("Laterne defekt"), "Titel" },
                { new StringContent("Seit Tagen dunkel"), "Beschreibung" },
                { new StringContent("49.45"), "Latitude" },
                { new StringContent("11.08"), "Longitude" },
                { new StringContent(tokens.RequestToken!), "__RequestVerificationToken" },
            };
            var datei = new ByteArrayContent(foto);
            datei.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(datei, "Foto", "foto.jpg");
            var request = new HttpRequestMessage(HttpMethod.Post, "/Maengel/Create") { Content = form };
            request.Headers.Add("Cookie", $"{options.Cookie.Name}={tokens.CookieToken}");
            return request;
        }

        [Fact]
        public async Task Foto_geht_als_Base64_mit_Typ_an_die_API()
        {
            var api = new FakeApi();
            using var factory = new PortalFactory(api);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

            var response = await client.SendAsync(Melden(factory, Jpeg));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var body = Assert.Single(api.Bodies);
            Assert.Contains($"\"fotoBase64\":\"{Convert.ToBase64String(Jpeg)}\"", body);
            Assert.Contains("\"fotoContentType\":\"image/jpeg\"", body);
        }

        [Fact]
        public async Task Zu_grosses_Foto_wird_nicht_weitergegeben()
        {
            var api = new FakeApi();
            using var factory = new PortalFactory(api);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

            var response = await client.SendAsync(Melden(factory, new byte[2 * 1024 * 1024 + 1]));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // Formular mit Fehlermeldung
            Assert.Contains("zu gro", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            Assert.Empty(api.Bodies);
        }

        [Fact]
        public async Task Meine_Meldungen_mit_Foto_vom_eigenen_Server()
        {
            using var factory = new PortalFactory(new FakeApi());
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

            var html = await client.GetStringAsync("/Maengel");
            var foto = await client.GetAsync($"/Maengel/Foto/{Meldung}");

            Assert.Contains("Laterne defekt", html);
            Assert.Contains($"src=\"/Maengel/Foto/{Meldung}\"", html);
            Assert.Contains("11.10.2026, 10:00", html);   // deutsche Zeit
            Assert.Equal("image/jpeg", foto.Content.Headers.ContentType!.MediaType);
            Assert.Equal(Jpeg, await foto.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Maengel/Foto/{Guid.NewGuid()}")).StatusCode);
        }

        [Fact]
        public async Task Teilen_ohne_Service_Worker_fuehrt_zum_Formular_Manifest_kennt_das_Ziel()
        {
            using var factory = new PortalFactory(new FakeApi());
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            var teilen = new MultipartFormDataContent { { new ByteArrayContent(Jpeg), "foto", "foto.jpg" } };

            var response = await client.PostAsync("/Maengel/Teilen", teilen);
            var manifest = await client.GetStringAsync("/manifest.webmanifest");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Maengel/Create", response.Headers.Location!.OriginalString);
            Assert.Contains("\"share_target\"", manifest);
            Assert.Contains("\"action\": \"/Maengel/Teilen\"", manifest);
        }
    }
}
