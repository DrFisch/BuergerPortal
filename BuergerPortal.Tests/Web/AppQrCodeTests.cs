using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace BuergerPortal.Tests.Web
{
    /// <summary>QR-Code „Auf dem Handy öffnen“ (/app/qr.svg): ohne Anmeldung, als SVG vom eigenen Server.</summary>
    public class AppQrCodeTests
    {
        private sealed class PortalFactory : WebApplicationFactory<HomeController>
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
                }));
            }
        }

        [Fact]
        public async Task QR_Code_ohne_Anmeldung_als_SVG()
        {
            using var factory = new PortalFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync("/app/qr.svg");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("<svg", await response.Content.ReadAsStringAsync());
        }
    }
}
