using BuergerPortal.Web.Controllers;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// X5: Sitzungs- und Antiforgery-Cookie des Portals tragen das Präfix <c>__Host-</c> und erfüllen dessen Regeln
    /// (Secure, Path=/, kein Domain-Attribut) – sonst würde der Browser sie verwerfen.
    /// </summary>
    public class HostCookieTests
    {
        private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            public const string SchemeName = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
                Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "test-person"), new Claim("name", "Test Person")], SchemeName, "name", "role")), SchemeName)));
        }

        private sealed class PortalFactory : WebApplicationFactory<HomeController>
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
                builder.ConfigureTestServices(services => services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
            }
        }

        [Fact]
        public async Task Antiforgery_Cookie_mit_Host_Praefix()
        {
            using var factory = new PortalFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

            var response = await client.GetAsync("/Maengel/Create");

            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-bp-antiforgery=", StringComparison.Ordinal));
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/;", cookie + ";", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sitzungscookie_mit_Host_Praefix()
        {
            using var factory = new PortalFactory();
            var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme);

            var cookie = options.Cookie.Build(new DefaultHttpContext());

            Assert.Equal(PortalSession.CookieName, options.Cookie.Name);
            Assert.StartsWith("__Host-", options.Cookie.Name);
            Assert.True(cookie.Secure);
            Assert.Equal("/", cookie.Path);
            Assert.Null(cookie.Domain);
        }
    }
}
