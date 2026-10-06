using BuergerPortal.PostkorbSimulation.Api;
using BuergerPortal.PostkorbSimulation.Controllers;
using BuergerPortal.PostkorbSimulation.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;

namespace BuergerPortal.Tests.Postkorb
{
    /// <summary>REST-Schnittstelle und Zugriffsschutz der Postkorb-Simulation (echter Host, InMemory-Datenbank).</summary>
    public class PostkorbApiTests(PostkorbApiTests.Factory factory) : IClassFixture<PostkorbApiTests.Factory>
    {
        public sealed class Factory : WebApplicationFactory<MessagesApiController>
        {
            public const string Key = "test-schluessel-mit-mindestens-32-zeichen";
            private readonly string database = Guid.NewGuid().ToString();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{PostkorbApiOptions.SectionName}:ApiKey"] = Key,
                }));
                builder.ConfigureServices(services =>
                {
                    // SQL Server durch eine InMemory-Datenbank ersetzen.
                    services.RemoveAll<DbContextOptions<PostkorbDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<PostkorbDbContext>>();
                    services.AddDbContext<PostkorbDbContext>(o => o.UseInMemoryDatabase(database));
                });
            }
        }

        private static readonly Guid Handle = Guid.Parse("11b2dc8f-3831-3b26-afde-aa0be42bd79b");

        private static object Message(Guid? handle = null) => new
        {
            mailboxUuid = handle ?? Handle,
            title = "Terminbestätigung",
            content = "Ihr Termin ist gebucht.",
            sender = "Testbehörde",
            service = "Terminvereinbarung",
            storkQaaLevel = 1,
        };

        private HttpClient Client(string? apiKey)
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            if (apiKey != null) client.DefaultRequestHeaders.Add(RequireApiKeyAttribute.HeaderName, apiKey);
            return client;
        }

        [Fact]
        public async Task Ohne_API_Schluessel_401()
        {
            var response = await Client(null).PostAsJsonAsync("/api/v1/messages", Message());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Falscher_API_Schluessel_401()
        {
            var response = await Client("falscher-schluessel-falscher-schluessel").PostAsJsonAsync("/api/v1/messages", Message());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Gueltige_Nachricht_wird_gespeichert()
        {
            var response = await Client(Factory.Key).PostAsJsonAsync("/api/v1/messages", Message());

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var id = (await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>())!["id"];
            using var scope = factory.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<PostkorbDbContext>().Messages.FindAsync(id);
            Assert.NotNull(stored);
            Assert.Equal(Handle, stored.MailboxUuid);
            Assert.Equal("Testbehörde", stored.Sender);
            Assert.Null(stored.ReadUtc);
        }

        [Fact]
        public async Task Leeres_Postkorb_Handle_400()
        {
            var response = await Client(Factory.Key).PostAsJsonAsync("/api/v1/messages", Message(Guid.Empty));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Postfach_ohne_Anmeldung_fuehrt_zur_BundID()
        {
            var response = await Client(null).GetAsync("/Postfach");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/bundid/login", response.Headers.Location!.PathAndQuery);
        }

        [Fact]
        public async Task Nachricht_ohne_Anmeldung_fuehrt_zur_BundID_auch_mit_API_Schluessel()
        {
            // Der API-Schlüssel gilt nur für die REST-Schnittstelle, nicht für die Postfach-Oberfläche.
            var response = await Client(Factory.Key).GetAsync($"/Postfach/Nachricht/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Theory]
        [InlineData("richtiger-schluessel-richtiger-schluessel", "richtiger-schluessel-richtiger-schluessel", true)]
        [InlineData("richtiger-schluessel-richtiger-schluessel", "richtiger-schluessel-richtiger-schluesseL", false)]
        [InlineData("", "richtiger-schluessel-richtiger-schluessel", false)]
        [InlineData("kurz", "richtiger-schluessel-richtiger-schluessel", false)]
        public void Schluesselvergleich(string provided, string expected, bool matches)
        {
            Assert.Equal(matches, RequireApiKeyAttribute.Matches(provided, expected));
        }
    }
}
