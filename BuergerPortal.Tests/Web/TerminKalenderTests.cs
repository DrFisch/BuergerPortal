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
    /// „Zum Kalender hinzufügen“: eigener Termin als .ics, stornierte und fremde Termine nicht (echter Host des Portals,
    /// Anmeldung durch ein Test-Schema ersetzt, API nachgebildet).
    /// </summary>
    public class TerminKalenderTests
    {
        internal static readonly Guid Gebucht = Guid.Parse("6f9d2c1e-0000-4000-8000-000000000001");
        internal static readonly Guid Storniert = Guid.Parse("6f9d2c1e-0000-4000-8000-000000000002");
        // Relativ zu heute, damit die Tests nicht mit dem Kalender veralten (der Einchecklink läuft einen Tag nach Ende ab).
        internal static readonly DateTime StartUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(9).AddMinutes(15);

        private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
                Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "test-person"), new Claim("name", "Test Person")], SchemeName, "name", "role")), SchemeName)));
        }

        // Nachbildung der API: GET api/appointments/{id} liefert nur die eigenen Termine (sonst 404).
        internal sealed class FakeApi : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var path = request.RequestUri!.AbsolutePath;
                foreach (var (id, cancelled) in new[] { (Gebucht, false), (Storniert, true) })
                {
                    if (path == $"/api/appointments/{id}" || path == $"/api/appointments/{id}/checkin")
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(new
                            {
                                id,
                                service = 1,
                                location = 1,
                                startUtc = StartUtc,
                                endUtc = StartUtc.AddMinutes(15),
                                cancelled,
                            }),
                        });
                    }
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        internal sealed class PortalFactory(bool authenticated = true) : WebApplicationFactory<HomeController>
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
                    if (authenticated)
                    {
                        services.AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    }
                    services.AddHttpClient("BuergerPortalApi").ConfigurePrimaryHttpMessageHandler(() => new FakeApi());
                });
            }
        }

        [Fact]
        public async Task Eigener_Termin_als_Kalenderdatei()
        {
            using var factory = new PortalFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync($"/Termine/Kalender/{Gebucht}");
            var ics = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(StartUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
            Assert.Equal($"termin-{startLocal:yyyy-MM-dd-HHmm}.ics", response.Content.Headers.ContentDisposition!.FileNameStar
                ?? response.Content.Headers.ContentDisposition.FileName);
            Assert.Contains($"DTSTART:{StartUtc:yyyyMMdd}T091500Z", ics);
            Assert.Contains($"UID:{Gebucht}@localhost", ics);
            Assert.Contains("Termin im Bürgeramt:", ics);
        }

        [Fact]
        public async Task Stornierter_oder_fremder_Termin_ergibt_404()
        {
            using var factory = new PortalFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Termine/Kalender/{Storniert}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Termine/Kalender/{Guid.NewGuid()}")).StatusCode);
        }
    }
}
