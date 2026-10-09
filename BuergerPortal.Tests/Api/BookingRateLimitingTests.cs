using BuergerPortal.Api.Controllers;
using BuergerPortal.Api.Extensions;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Buchungen je Person begrenzen: nach der Grenze 429 mit verständlicher Meldung, andere Person nicht betroffen,
    /// Lesen nicht begrenzt (echter API-Host, InMemory-Datenbank, Bearer-Anmeldung durch ein Test-Schema ersetzt).
    /// </summary>
    public class BookingRateLimitingTests
    {
        // Ersetzt die Prüfung des Access-Tokens: Header "X-Test-Sub" = Person
        private sealed class TestBearerHandler(IOptionsMonitor<JwtBearerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<JwtBearerOptions>(options, logger, encoder)
        {
            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (!Request.Headers.TryGetValue("X-Test-Sub", out var sub)) return Task.FromResult(AuthenticateResult.NoResult());
                var identity = new ClaimsIdentity([new Claim("sub", sub!), new Claim("scope", "buergerportal_api")], JwtBearerDefaults.AuthenticationScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), JwtBearerDefaults.AuthenticationScheme)));
            }
        }

        private sealed class ApiFactory : WebApplicationFactory<AppointmentsController>
        {
            private readonly string database = Guid.NewGuid().ToString();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:Audience"] = "buergerportal_api",
                    ["Postkorb:BaseUrl"] = "http://postfach.invalid/",
                    ["Postkorb:ApiKey"] = "test-schluessel-mit-mindestens-32-zeichen",
                    ["Database:MigrateOnStartup"] = "false",
                    ["RateLimiting:BuchungenProMinute"] = "2",
                }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PortalDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<PortalDbContext>>();
                    services.AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(database));
                });
                builder.ConfigureTestServices(services => services.PostConfigure<AuthenticationOptions>(o =>
                    o.SchemeMap[JwtBearerDefaults.AuthenticationScheme].HandlerType = typeof(TestBearerHandler)));
            }
        }

        private static readonly string PersonA = Guid.NewGuid().ToString();   // sub ist im Portal eine GUID

        private static HttpRequestMessage Book(string sub)
        {
            // Inhalt egal (ungültig → 400): Die Grenze greift vor der Prüfung des Inhalts.
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/appointments") { Content = JsonContent.Create(new { }) };
            request.Headers.Add("X-Test-Sub", sub);
            return request;
        }

        [Fact]
        public async Task Nach_zwei_Buchungen_je_Minute_folgt_429_mit_Meldung()
        {
            using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(PersonA))).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(PersonA))).StatusCode);
            var rejected = await client.SendAsync(Book(PersonA));

            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("60", rejected.Headers.GetValues("Retry-After").Single());
            var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(BookingRateLimiting.RejectedDetail, problem!.Detail);

            // eine andere Person ist nicht betroffen, Lesen ist nicht begrenzt
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(Book(Guid.NewGuid().ToString()))).StatusCode);
            var read = new HttpRequestMessage(HttpMethod.Get, "/api/appointments/mine");
            read.Headers.Add("X-Test-Sub", PersonA);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.SendAsync(read)).StatusCode);
        }
    }
}
