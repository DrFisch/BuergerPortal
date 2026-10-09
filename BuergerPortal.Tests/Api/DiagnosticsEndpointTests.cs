using BuergerPortal.Api.Controllers;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Diagnose-Controller der API (api/debug, api/email/test) gibt es nur in der Entwicklung – im Betrieb 404
    /// (echter Host der API, InMemory-Datenbank).
    /// </summary>
    public class DiagnosticsEndpointTests
    {
        private sealed class ApiFactory(string environment) : WebApplicationFactory<AppointmentsController>
        {
            private readonly string database = Guid.NewGuid().ToString();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:Audience"] = "buergerportal_api",
                    ["Postkorb:BaseUrl"] = "http://postfach.invalid/",
                    ["Postkorb:ApiKey"] = "test-schluessel-mit-mindestens-32-zeichen",
                    ["Database:MigrateOnStartup"] = "false",
                }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PortalDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<PortalDbContext>>();
                    services.AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(database));
                });
            }
        }

        [Fact]
        public async Task Im_Betrieb_gibt_es_die_Diagnose_Adressen_nicht()
        {
            using var factory = new ApiFactory("Production");
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/debug/auth-header")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/debug/whoami")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsJsonAsync("/api/email/test", new { to = "niemand@example.invalid" })).StatusCode);
        }

        [Fact]
        public async Task In_der_Entwicklung_vorhanden()
        {
            using var factory = new ApiFactory("Development");
            using var client = factory.CreateClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/debug/auth-header")).StatusCode);
        }
    }
}
