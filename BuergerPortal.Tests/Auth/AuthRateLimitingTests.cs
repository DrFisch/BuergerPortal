using AuthenticationServer.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace BuergerPortal.Tests.Auth
{
    /// <summary>
    /// Rate-Limiting des Auth-Servers: Grenzen je Bereich (SAML, OIDC, Server-zu-Server) und Client-IP, 429 mit
    /// Retry-After, nicht begrenzte Pfade (kleiner Host mit genau der Registrierung aus Program.cs).
    /// </summary>
    public class AuthRateLimitingTests
    {
        private static async Task<WebApplication> StartAsync(int samlLimit)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:Saml"] = samlLimit.ToString() });
            builder.Services.AddAuthRateLimiting(builder.Configuration);
            var app = builder.Build();
            // Im Test kommt die Client-IP aus einem Header (im Betrieb aus X-Forwarded-For über UseForwardedHeaders).
            app.Use((ctx, next) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Ip", out var ip)) ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip!);
                return next();
            });
            app.UseRateLimiter();
            app.MapPost("/bundid/acs", () => "ok");
            app.MapGet("/bundid/metadata", () => "ok");
            await app.StartAsync();
            return app;
        }

        private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string ip)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-Ip", ip);
            return client.SendAsync(request);
        }

        [Fact]
        public async Task Nach_der_Grenze_folgt_429_mit_Retry_After()
        {
            await using var app = await StartAsync(samlLimit: 3);
            var client = app.GetTestClient();
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/bundid/acs", "203.0.113.7")).StatusCode);
            }

            var rejected = await Send(client, HttpMethod.Post, "/bundid/acs", "203.0.113.7");
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("60", rejected.Headers.GetValues("Retry-After").Single());
            Assert.Contains("Zu viele Anmeldeversuche", await rejected.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Andere_IP_hat_ihre_eigene_Grenze()
        {
            await using var app = await StartAsync(samlLimit: 1);
            var client = app.GetTestClient();
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/bundid/acs", "203.0.113.7")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(client, HttpMethod.Post, "/bundid/acs", "203.0.113.7")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/bundid/acs", "198.51.100.9")).StatusCode);
        }

        [Fact]
        public async Task Metadaten_sind_nicht_begrenzt()
        {
            await using var app = await StartAsync(samlLimit: 1);
            var client = app.GetTestClient();
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Get, "/bundid/metadata", "203.0.113.7")).StatusCode);
            }
        }

        [Theory]
        [InlineData("/bundid/acs", "saml")]
        [InlineData("/bundid/login", "saml")]
        [InlineData("/connect/authorize", "oidc")]
        [InlineData("/connect/logout", "oidc")]
        [InlineData("/connect/token", "backchannel")]
        [InlineData("/connect/userinfo", "backchannel")]
        [InlineData("/bundid/metadata", null)]
        [InlineData("/Identity/Account/Login", null)]
        public void Bereiche_je_Pfad(string path, string? area)
        {
            Assert.Equal(area, AuthRateLimiting.For(new PathString(path), new AuthRateLimiting.Limits())?.Area);
        }
    }
}
