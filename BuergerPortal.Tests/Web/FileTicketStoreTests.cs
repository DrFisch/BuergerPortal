using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Serverseitige Sitzungsdaten: Cookie trägt nur die Kennung, Abmelden löscht die Sitzung.</summary>
    public sealed class FileTicketStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "bpsim-tickettest-" + Guid.NewGuid().ToString("N"));
        private readonly IDataProtectionProvider _dp = new EphemeralDataProtectionProvider();

        private FileTicketStore Store() => new(_dir, _dp, NullLogger<FileTicketStore>.Instance);

        private static AuthenticationTicket Ticket(string name)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", name)], "Cookies"));
            var props = new AuthenticationProperties();
            props.StoreTokens([new AuthenticationToken { Name = "refresh_token", Value = "rt-" + name }]);
            return new AuthenticationTicket(principal, props, "Cookies");
        }

        [Fact]
        public async Task Speichert_liest_erneuert_und_loescht()
        {
            var store = Store();
            var key = await store.StoreAsync(Ticket("Manfred Schmidt"));

            Assert.Matches("^[0-9A-F]{64}$", key);
            var gelesen = await Store().RetrieveAsync(key);   // auch eine neue Instanz (z. B. nach Neustart)
            Assert.Equal("Manfred Schmidt", gelesen!.Principal.FindFirstValue("name"));
            Assert.Equal("rt-Manfred Schmidt", gelesen.Properties.GetTokenValue("refresh_token"));

            await store.RenewAsync(key, Ticket("Anna Beispiel"));
            Assert.Equal("Anna Beispiel", (await store.RetrieveAsync(key))!.Principal.FindFirstValue("name"));

            await store.RemoveAsync(key);
            Assert.Null(await store.RetrieveAsync(key));
        }

        [Fact]
        public async Task Daten_liegen_verschluesselt_und_fremde_Kennungen_werden_abgewiesen()
        {
            var store = Store();
            var key = await store.StoreAsync(Ticket("Manfred Schmidt"));
            var inhalt = File.ReadAllText(Directory.GetFiles(_dir, "*.ticket").Single());

            Assert.DoesNotContain("Manfred", inhalt);
            Assert.Null(await store.RetrieveAsync("../../etc/passwd"));
            Assert.Null(await store.RetrieveAsync(new string('A', 64)));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
    }
}
