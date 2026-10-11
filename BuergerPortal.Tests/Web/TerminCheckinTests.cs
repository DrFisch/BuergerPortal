using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Seite nach dem Scannen des QR-Codes: ohne Anmeldung, ohne Personendaten, Status aus der API (echter Host des
    /// Portals, API nachgebildet).
    /// </summary>
    public class TerminCheckinTests
    {
        private static async Task<string> PageAsync(Guid appointmentId)
        {
            using var factory = new TerminKalenderTests.PortalFactory(authenticated: false);
            var token = factory.Services.GetRequiredService<TerminCheckinLink>()
                .Create(appointmentId, TerminKalenderTests.StartUtc.AddMinutes(15));
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync($"/Termine/Einchecken/{token}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Ohne_Anmeldung_gueltiger_Termin_mit_Leistung_Ort_Zeit()
        {
            var html = await PageAsync(TerminKalenderTests.Gebucht);

            Assert.Contains("data-status=\"Gueltig\"", html);
            Assert.Contains("Bürgeramt Mitte", html);
            // Uhrzeit in deutscher Zeit (09:15 UTC ist je nach Jahreszeit 10:15 oder 11:15)
            var startLocal = TimeZoneInfo.ConvertTimeFromUtc(TerminKalenderTests.StartUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
            Assert.Contains(startLocal.ToString("HH:mm"), html);
            Assert.DoesNotContain("Test Person", html);
        }

        [Fact]
        public async Task Stornierter_Termin_und_unbekannter_Termin()
        {
            Assert.Contains("data-status=\"Storniert\"", await PageAsync(TerminKalenderTests.Storniert));
            Assert.Contains("data-status=\"Ungueltig\"", await PageAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task Ungueltige_Kennung_ohne_Anfrage_an_die_API()
        {
            using var factory = new TerminKalenderTests.PortalFactory(authenticated: false);
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var html = await client.GetStringAsync("/Termine/Einchecken/erfunden");

            Assert.Contains("data-status=\"Ungueltig\"", html);
        }
    }
}
