using BuergerPortal.Web.Controllers;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Content-Security-Policy des Portals: Nonce je Antwort, jedes &lt;script&gt; der Seite trägt genau diese Nonce,
    /// Schalter zwischen "nur melden" und "durchsetzen" (echter Host des Portals, Einstiegsseite ohne Anmeldung).
    /// </summary>
    public class ContentSecurityPolicyTests
    {
        private sealed class PortalFactory(string? header) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "https://auth.example.org",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                    ["Csp:Header"] = header,
                }));
            }
        }

        private static async Task<HttpResponseMessage> Start(string? header)
        {
            using var factory = new PortalFactory(header);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            return await client.GetAsync("/");
        }

        private static string NonceOf(string policy) => Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;

        [Fact]
        public async Task Standard_meldet_nur_und_jedes_Skript_traegt_die_Nonce()
        {
            var response = await Start(null);
            var policy = Assert.Single(response.Headers.GetValues(ContentSecurityPolicy.ReportOnlyHeader));
            Assert.False(response.Headers.Contains(ContentSecurityPolicy.EnforceHeader));

            var nonce = NonceOf(policy);
            Assert.True(nonce.Length >= 20, "Nonce fehlt oder ist zu kurz");
            var html = await response.Content.ReadAsStringAsync();
            var scripts = Regex.Matches(html, "<script[^>]*>").Select(m => m.Value).ToList();
            Assert.NotEmpty(scripts);
            Assert.All(scripts, tag => Assert.Contains($"nonce=\"{nonce}\"", tag));
        }

        [Fact]
        public async Task Jede_Antwort_hat_eine_neue_Nonce()
        {
            var first = NonceOf((await Start(null)).Headers.GetValues(ContentSecurityPolicy.ReportOnlyHeader).Single());
            var second = NonceOf((await Start(null)).Headers.GetValues(ContentSecurityPolicy.ReportOnlyHeader).Single());
            Assert.NotEqual(first, second);
        }

        [Fact]
        public async Task Schalter_setzt_die_Regeln_durch()
        {
            var response = await Start(ContentSecurityPolicy.EnforceHeader);
            Assert.True(response.Headers.Contains(ContentSecurityPolicy.EnforceHeader));
            Assert.False(response.Headers.Contains(ContentSecurityPolicy.ReportOnlyHeader));
        }

        [Fact]
        public void Regeln_erlauben_Formulare_nur_an_Portal_und_Auth_Server()
        {
            var policy = ContentSecurityPolicy.Build("abc", "https://auth.example.org");
            Assert.Contains("form-action 'self' https://auth.example.org", policy);
            Assert.Contains("frame-ancestors 'none'", policy);
            Assert.Contains("script-src 'self' 'nonce-abc'", policy);
            Assert.DoesNotContain("unsafe-inline", policy.Split("; ")[1]);   // script-src ohne unsafe-inline
        }
    }
}
