using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Abfrage für das Einchecken (QR-Code im Bürgeramt): ohne Anmeldung, aber ohne Personenbezug (echter API-Host,
    /// InMemory-Datenbank).
    /// </summary>
    public class AppointmentCheckinTests
    {
        private static async Task<Guid> SeedAsync(ApiTestFactory factory, AppointmentStatus status = AppointmentStatus.Booked)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                Service = ServiceType.AllgemeineBeratung,
                Location = LocationType.BuergermtNord,
                StartUtc = new DateTime(2026, 10, 13, 9, 15, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 10, 13, 9, 30, 0, DateTimeKind.Utc),
                UserId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                Status = status,
                AntragId = Guid.Parse("99999999-8888-7777-6666-555555555555"),
                RowVersion = [1],
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment.Id;
        }

        [Fact]
        public async Task Ohne_Anmeldung_Leistung_Ort_Zeit_und_Status_ohne_Personenbezug()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory);

            var response = await factory.CreateClient().GetAsync($"/api/appointments/{id}/checkin");
            var json = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"location\":2", json);
            Assert.Contains("\"cancelled\":false", json);
            Assert.DoesNotContain("11111111-2222-3333-4444-555555555555", json);   // Person
            Assert.DoesNotContain("99999999-8888-7777-6666-555555555555", json);   // Antrag
        }

        [Fact]
        public async Task Stornierter_Termin_wird_als_storniert_gemeldet_unbekannter_404()
        {
            using var factory = new ApiTestFactory();
            var id = await SeedAsync(factory, AppointmentStatus.Cancelled);
            var client = factory.CreateClient();

            Assert.Contains("\"cancelled\":true", await client.GetStringAsync($"/api/appointments/{id}/checkin"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/appointments/{Guid.NewGuid()}/checkin")).StatusCode);
        }
    }
}
