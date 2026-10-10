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
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Push-Aufrufe des Portals (/push/…) gehen an die API weiter; ändernde Aufrufe nur mit Antiforgery-Token
    /// (echter Host des Portals, Anmeldung durch ein Test-Schema ersetzt, API nachgebildet).
    /// </summary>
    public class PushProxyTests
    {
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
            public List<string> Calls { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
                return Task.FromResult(request.RequestUri.AbsolutePath == "/api/push/key"
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { publicKey = "BKey" }) }
                    : new HttpResponseMessage(HttpStatusCode.NoContent));
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

        private static HttpRequestMessage Subscribe() => new(HttpMethod.Post, "/push/subscriptions")
        {
            Content = JsonContent.Create(new { endpoint = "https://fcm.googleapis.com/fcm/send/x", keys = new { p256dh = "B", auth = "A" } }),
        };

        [Fact]
        public async Task Schluessel_kommt_von_der_API()
        {
            var api = new FakeApi();
            using var factory = new PortalFactory(api);

            var key = await factory.CreateClient().GetFromJsonAsync<Dictionary<string, string>>("/push/key");

            Assert.Equal("BKey", key!["publicKey"]);
            Assert.Equal(["GET /api/push/key"], api.Calls);
        }

        [Fact]
        public async Task Ohne_Antiforgery_Token_wird_nichts_weitergereicht()
        {
            var api = new FakeApi();
            using var factory = new PortalFactory(api);

            var response = await factory.CreateClient().SendAsync(Subscribe());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(api.Calls);
        }

        [Fact]
        public async Task Mit_Antiforgery_Token_geht_das_Abo_an_die_API()
        {
            var api = new FakeApi();
            using var factory = new PortalFactory(api);
            var antiforgery = factory.Services.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetTokens(new DefaultHttpContext { RequestServices = factory.Services, User = Person() });
            var options = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
            var request = Subscribe();
            request.Headers.Add("Cookie", $"{options.Cookie.Name}={tokens.CookieToken}");
            request.Headers.Add(options.HeaderName!, tokens.RequestToken);

            var response = await factory.CreateClient().SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(["POST /api/push/subscriptions"], api.Calls);
        }
    }
}
