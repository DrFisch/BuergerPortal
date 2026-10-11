using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Zeitleiste in den Antragsdetails: Zeitpunkte aus dem Statusverlauf in deutscher Zeit, Hinweis auf die simulierte
    /// Bearbeitung (echter Host des Portals, API nachgebildet).
    /// </summary>
    public class AntragZeitleisteTests
    {
        private static readonly Guid Antrag = Guid.Parse("7a000000-0000-4000-8000-000000000001");

        private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
                Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "test-person"), new Claim("name", "Test Person")], SchemeName, "name", "role")), SchemeName)));
        }

        // Antrag in Bearbeitung: angelegt 07:55, eingereicht 08:00, in Bearbeitung 08:02 (UTC, ohne Zeitzonenkennung wie die API)
        private sealed class FakeApi(int status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                object? body = request.RequestUri!.AbsolutePath switch
                {
                    var p when p == $"/api/antraege/reisepass/{Antrag}" => new
                    {
                        id = Antrag, typ = 1, vorname = "Tina", nachname = "Test", geburtsdatum = "1990-01-01",
                        status, createdUtc = "2026-10-11T07:55:00", submittedUtc = "2026-10-11T08:00:00",
                    },
                    var p when p == $"/api/antraege/{Antrag}/verlauf" => new object[]
                    {
                        new { status = 2, zeitpunktUtc = "2026-10-11T08:00:00Z" },
                        new { status = 3, zeitpunktUtc = "2026-10-11T08:02:00Z" },
                    },
                    _ => null,
                };
                return Task.FromResult(body is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
            }
        }

        private sealed class PortalFactory(int status) : WebApplicationFactory<HomeController>
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
                    services.AddHttpClient("BuergerPortalApi").ConfigurePrimaryHttpMessageHandler(() => new FakeApi(status));
                });
            }
        }

        [Fact]
        public async Task Zeitleiste_zeigt_Zeitpunkte_in_deutscher_Zeit_und_den_Simulationshinweis()
        {
            using var factory = new PortalFactory(status: 3);
            var html = WebUtility.HtmlDecode(await factory.CreateClient().GetStringAsync($"/Antraege/Antrag/{Antrag}"));

            // 07:55/08:00/08:02 UTC = 09:55/10:00/10:02 Uhr Sommerzeit
            Assert.Contains("11.10.2026 09:55", html);
            Assert.Contains("11.10.2026 10:00", html);
            Assert.Contains("11.10.2026 10:02", html);
            Assert.Contains("id=\"simulationHinweis\"", html);
        }

        [Fact]
        public async Task Abgeschlossener_Antrag_ohne_Simulationshinweis()
        {
            using var factory = new PortalFactory(status: 4);
            var html = await factory.CreateClient().GetStringAsync($"/Antraege/Antrag/{Antrag}");

            Assert.DoesNotContain("simulationHinweis", html);
        }
    }
}
