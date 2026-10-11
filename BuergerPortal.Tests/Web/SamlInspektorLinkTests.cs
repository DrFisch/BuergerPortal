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
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Weg zum SAML-Inspektor aus „Meine Daten“: Link nur, wenn der Inspektor an ist; die Weiterleitung geht immer zur
    /// konfigurierten Adresse des Auth-Servers (kein Ziel aus der Anfrage).
    /// </summary>
    public class SamlInspektorLinkTests
    {
        private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var identity = new ClaimsIdentity([new Claim("sub", "test-person"), new Claim("name", "Test Person")], SchemeName, "name", "role");
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
            }
        }

        private sealed class PortalFactory(bool inspektorEnabled) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "https://auth.example.test/",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                    ["BundId:Inspektor:Enabled"] = inspektorEnabled.ToString(),
                }));
                builder.ConfigureTestServices(services =>
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
            }
        }

        private static HttpClient Client(PortalFactory factory) =>
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        [Fact]
        public async Task Meine_Daten_verlinkt_den_Inspektor_des_Auth_Servers()
        {
            using var factory = new PortalFactory(inspektorEnabled: true);

            var html = await Client(factory).GetStringAsync("/MeineDaten");

            Assert.Contains("href=\"https://auth.example.test/bundid/inspektor\"", html);
            Assert.Contains("/MeineDaten/SamlInspektorNeu", html);
        }

        [Fact]
        public async Task Weiterleitung_geht_zur_konfigurierten_Adresse()
        {
            using var factory = new PortalFactory(inspektorEnabled: true);

            var response = await Client(factory).GetAsync("/MeineDaten/SamlInspektor?url=https://boese.example.test/");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("https://auth.example.test/bundid/inspektor", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Abgeschaltet_kein_Link_und_404()
        {
            using var factory = new PortalFactory(inspektorEnabled: false);
            var client = Client(factory);

            Assert.DoesNotContain("/bundid/inspektor", await client.GetStringAsync("/MeineDaten"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/MeineDaten/SamlInspektor")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/MeineDaten/SamlInspektorNeu")).StatusCode);
        }
    }
}
