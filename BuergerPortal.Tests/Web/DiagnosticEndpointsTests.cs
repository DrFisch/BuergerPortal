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
    /// Diagnose-Adressen des Portals (/auth/debug, /home/testuser) gibt es nur in der Entwicklung – im Betrieb 404,
    /// auch für angemeldete Personen (echter Host des Portals, Anmeldung durch ein Test-Schema ersetzt).
    /// </summary>
    public class DiagnosticEndpointsTests
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

        private sealed class PortalFactory(string environment, bool? enabled) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                    ["Diagnostics:Enabled"] = enabled?.ToString(),
                }));
                builder.ConfigureTestServices(services =>
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
            }
        }

        private static async Task<HttpStatusCode> Get(string environment, bool? enabled, string path)
        {
            using var factory = new PortalFactory(environment, enabled);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            return (await client.GetAsync(path)).StatusCode;
        }

        [Theory]
        [InlineData("/auth/debug")]
        [InlineData("/home/testuser")]
        public async Task Im_Betrieb_gibt_es_die_Adresse_nicht(string path)
        {
            Assert.Equal(HttpStatusCode.NotFound, await Get("Production", null, path));
        }

        [Theory]
        [InlineData("/auth/debug")]
        [InlineData("/home/testuser")]
        public async Task In_der_Entwicklung_vorhanden(string path)
        {
            Assert.Equal(HttpStatusCode.OK, await Get("Development", null, path));
        }

        [Fact]
        public async Task Schalter_ueberstimmt_die_Umgebung()
        {
            Assert.Equal(HttpStatusCode.OK, await Get("Production", true, "/home/testuser"));
            Assert.Equal(HttpStatusCode.NotFound, await Get("Development", false, "/home/testuser"));
        }
    }
}
