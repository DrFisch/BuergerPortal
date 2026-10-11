using AuthenticationServer.BundId;
using AuthenticationServer.Controllers;
using BuergerPortal.BundId;
using BuergerPortal.Tests.BundId;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace BuergerPortal.Tests.Auth
{
    /// <summary>
    /// Seite des SAML-Inspektors im Auth-Server (kleiner Host mit Controller und Razor-Seiten des Auth-Servers):
    /// abgeschaltet 404, ohne Anmeldung ein Hinweis, die eigene Anmeldung mit Prüfungen – ohne Tokens und ohne
    /// einlösbare Signatur.
    /// </summary>
    public class BundIdInspektorPageTests
    {
        private static async Task<WebApplication> StartAsync(bool enabled, BundIdInspection? inspection = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.Configure<BundIdInspektorOptions>(o =>
            {
                o.Enabled = enabled;
                o.PortalUrl = "https://portal.example.test/";
            });
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<BundIdInspectionStore>();
            var authAssembly = typeof(BundIdInspektorController).Assembly;
            builder.Services.AddControllersWithViews()
                .AddApplicationPart(authAssembly)
                .ConfigureApplicationPartManager(m => m.ApplicationParts.Add(new CompiledRazorAssemblyPart(authAssembly)));
            var app = builder.Build();
            // Im Test meldet der Header X-Test-User an (im Betrieb: Identity-Cookie nach der BundID-Anmeldung).
            app.Use((ctx, next) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-User", out var user))
                {
                    ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user!)], "Test"));
                }
                return next();
            });
            // Stellvertretend für den ACS: Protokoll der Anmeldung für "konto-1" aufbewahren.
            app.MapPost("/test/acs", (HttpContext ctx, BundIdInspectionStore store) =>
            {
                store.Save(ctx.Response, "konto-1", inspection!);
                return Results.Ok();
            });
            app.MapControllers();
            await app.StartAsync();
            return app;
        }

        private static (BundIdInspection Inspection, string OriginalXml) CreateInspection()
        {
            var samlResponse = SamlTestIdp.CreateResponse();
            var config = SamlTestIdp.CreateSpConfiguration();
            var response = SamlTestIdp.Validate(samlResponse, config);
            var options = new BundIdOptions { SpEntityId = SamlTestIdp.SpEntityId, AssertionConsumerServiceUrl = SamlTestIdp.Acs.ToString() };
            var attributes = BundIdAttributes.FromSaml(response.ClaimsIdentity, options.PostkorbHandleAttribute);
            var inspection = BundIdInspection.Create(response, config,
                new BundIdLoginState(SamlTestIdp.RequestId, TrustLevel.Normal, "/connect/authorize?state=abc"), attributes, options, DateTime.UtcNow);
            return (inspection, Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse)));
        }

        private static async Task<HttpResponseMessage> Get(HttpClient client, string? user, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/bundid/inspektor");
            if (user != null) request.Headers.Add("X-Test-User", user);
            if (cookie != null) request.Headers.Add("Cookie", cookie);
            return await client.SendAsync(request);
        }

        [Fact]
        public async Task Abgeschaltet_antwortet_404()
        {
            await using var app = await StartAsync(enabled: false);

            Assert.Equal(HttpStatusCode.NotFound, (await Get(app.GetTestClient(), "konto-1")).StatusCode);
        }

        [Fact]
        public async Task Ohne_Anmeldung_ein_Hinweis_ohne_Protokoll()
        {
            await using var app = await StartAsync(enabled: true);

            var response = await Get(app.GetTestClient(), user: null);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Gerade keine Anmeldung zu sehen", html);
            Assert.Contains("https://portal.example.test/MeineDaten/SamlInspektorNeu", html);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }

        [Fact]
        public async Task Eigene_Anmeldung_mit_Pruefungen_ohne_Tokens_und_ohne_einloesbare_Signatur()
        {
            var (inspection, originalXml) = CreateInspection();
            await using var app = await StartAsync(enabled: true, inspection);
            var client = app.GetTestClient();
            var cookie = (await client.PostAsync("/test/acs", null)).Headers.GetValues("Set-Cookie").Single().Split(';')[0];

            // Razor schreibt Umlaute als Entitäten (&#xE4;) – für den Vergleich dekodieren.
            var html = WebUtility.HtmlDecode(await (await Get(client, "konto-1", cookie)).Content.ReadAsStringAsync());

            Assert.Contains("Was geprüft wurde", html);
            Assert.Contains("Empfänger (Destination, Recipient)", html);
            Assert.Contains("tabindex=\"0\" role=\"region\" aria-labelledby=\"inspektor-pruefungen\"", html);   // per Tastatur scrollbar
            Assert.Contains("bundid_bpk2", html);
            Assert.Contains("BUND…-T01", html);
            Assert.DoesNotContain("BUNDIDSIM-T01", html);
            var signatureValue = Regex.Match(originalXml, "<SignatureValue>([^<]+)</SignatureValue>").Groups[1].Value;
            Assert.DoesNotContain(signatureValue, html);
            // Keine Tokens: weder JWT (beginnen mit "eyJ") noch Parameter der OIDC-Anfrage.
            Assert.DoesNotContain("eyJ", html);
            Assert.DoesNotContain("state=abc", html);
        }

        [Fact]
        public async Task Fremdes_Konto_sieht_die_Anmeldung_nicht()
        {
            var (inspection, _) = CreateInspection();
            await using var app = await StartAsync(enabled: true, inspection);
            var client = app.GetTestClient();
            var cookie = (await client.PostAsync("/test/acs", null)).Headers.GetValues("Set-Cookie").Single().Split(';')[0];

            var html = WebUtility.HtmlDecode(await (await Get(client, "konto-2", cookie)).Content.ReadAsStringAsync());

            Assert.Contains("Gerade keine Anmeldung zu sehen", html);
            Assert.DoesNotContain("Was geprüft wurde", html);
        }
    }
}
