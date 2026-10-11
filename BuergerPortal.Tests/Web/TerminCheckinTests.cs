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
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
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
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

            var html = await client.GetStringAsync("/Termine/Einchecken/erfunden");

            Assert.Contains("data-status=\"Ungueltig\"", html);
        }

        [Fact]
        public async Task Termindetails_zeigen_QR_Code_der_zur_Einchecken_Seite_fuehrt()
        {
            using var factory = new TerminKalenderTests.PortalFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

            var html = await client.GetStringAsync($"/Termine/Details/{TerminKalenderTests.Gebucht}");
            var url = System.Text.RegularExpressions.Regex.Match(html, "id=\"checkinQr\" data-url=\"([^\"]+)\"").Groups[1].Value;

            Assert.Contains("<svg", html);
            Assert.Contains("/Termine/Einchecken/", url);
            var token = url[(url.LastIndexOf('/') + 1)..];
            Assert.Equal(TerminKalenderTests.Gebucht, factory.Services.GetRequiredService<TerminCheckinLink>().Read(token));
        }

        [Fact]
        public async Task Stornierter_Termin_ohne_QR_Code_und_ohne_Kalenderdatei()
        {
            using var factory = new TerminKalenderTests.PortalFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

            var html = await client.GetStringAsync($"/Termine/Details/{TerminKalenderTests.Storniert}");

            Assert.DoesNotContain("checkinQr", html);
            Assert.DoesNotContain("kalenderLink", html);
        }
    }
}
