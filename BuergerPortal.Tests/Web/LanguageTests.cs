using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Sprache des Portals: Deutsch als Standard, auch wenn der Browser Englisch bevorzugt; Englisch nur nach Wahl.</summary>
    public class LanguageTests
    {
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
            }
        }

        private static async Task<string> StartPage(Action<HttpRequestMessage>? configure = null, string path = "/")
        {
            using var factory = new PortalFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            configure?.Invoke(request);
            return await (await client.SendAsync(request)).Content.ReadAsStringAsync();
        }

        [Fact]
        public async Task Englischer_Browser_bekommt_trotzdem_Deutsch()
        {
            var html = await StartPage(r => r.Headers.Add("Accept-Language", "en-US,en;q=0.9"));

            Assert.Contains("<html lang=\"de\"", html);
        }

        [Fact]
        public async Task Englisch_nur_nach_eigener_Wahl()
        {
            Assert.Contains("<html lang=\"en\"", await StartPage(r => r.Headers.Add("Cookie", ".AspNetCore.Culture=c%3Den%7Cuic%3Den")));
            Assert.Contains("<html lang=\"en\"", await StartPage(path: "/?culture=en"));
        }

        [Fact]
        public async Task Sprachwahl_steht_ohne_Anmeldung_in_der_Kopfzeile()
        {
            var html = await StartPage();

            Assert.Contains("/sprache/de?returnUrl=", html);
            Assert.Contains("/sprache/en?returnUrl=", html);
        }

        [Theory]
        [InlineData("/sprache/en?returnUrl=%2FHome%2FImpressum", "/Home/Impressum", "en")]
        [InlineData("/sprache/de?returnUrl=https%3A%2F%2Ffremd.example%2F", "/", "de")]      // kein Open Redirect
        [InlineData("/sprache/en?returnUrl=%2F%2Ffremd.example", "/", "en")]                 // protokollrelativ
        public async Task Sprachwahl_setzt_Cookie_und_fuehrt_nur_lokal_zurueck(string path, string location, string code)
        {
            using var factory = new PortalFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync(path);

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(location, response.Headers.Location!.OriginalString);
            Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith($".AspNetCore.Culture=c%3D{code}%7Cuic%3D{code}"));
        }

        [Fact]
        public async Task Unbekannte_Sprache_ergibt_404()
        {
            using var factory = new PortalFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("/sprache/fr")).StatusCode);
        }
    }
}
