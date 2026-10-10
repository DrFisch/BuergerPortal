using BuergerPortal.Api.Controllers;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Ersetzt die Prüfung des Access-Tokens: Header "X-Test-Sub" = Person (sub).</summary>
    public sealed class TestBearerHandler(IOptionsMonitor<JwtBearerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<JwtBearerOptions>(options, logger, encoder)
    {
        public const string Header = "X-Test-Sub";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var sub)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim("sub", sub!), new Claim("scope", "buergerportal_api")], JwtBearerDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), JwtBearerDefaults.AuthenticationScheme)));
        }
    }

    /// <summary>
    /// Echter API-Host für Tests: InMemory-Datenbank, Anmeldung über <see cref="TestBearerHandler"/>, eigene
    /// Konfigurationswerte und Dienste je Test.
    /// </summary>
    public sealed class ApiTestFactory(IDictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null)
        : WebApplicationFactory<AppointmentsController>
    {
        private readonly string database = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var values = new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "http://auth.invalid/",
                ["Authentication:Audience"] = "buergerportal_api",
                ["Postkorb:BaseUrl"] = "http://postfach.invalid/",
                ["Postkorb:ApiKey"] = "test-schluessel-mit-mindestens-32-zeichen",
                ["Database:MigrateOnStartup"] = "false",
            };
            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            {
                values[key] = value;
            }

            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(values));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<DbContextOptions<PortalDbContext>>();
                s.RemoveAll<IDbContextOptionsConfiguration<PortalDbContext>>();
                s.AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(database));
            });
            builder.ConfigureTestServices(s =>
            {
                s.PostConfigure<AuthenticationOptions>(o =>
                    o.SchemeMap[JwtBearerDefaults.AuthenticationScheme].HandlerType = typeof(TestBearerHandler));
                services?.Invoke(s);
            });
        }
    }

    public static class ApiTestRequests
    {
        /// <summary>Anfrage als angemeldete Person.</summary>
        public static HttpRequestMessage As(this HttpRequestMessage request, string sub)
        {
            request.Headers.Add(TestBearerHandler.Header, sub);
            return request;
        }
    }
}
