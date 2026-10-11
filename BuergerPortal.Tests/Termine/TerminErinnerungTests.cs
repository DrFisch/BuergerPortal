using BuergerPortal.Application.Appointments.Erinnerung;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using BuergerPortal.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>Erinnerung vor dem Termin: wer fällig ist, wer nicht, und jede Erinnerung nur einmal (InMemory-Datenbank).</summary>
    public class TerminErinnerungTests
    {
        private sealed class ManualTime(DateTimeOffset now) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = now;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static readonly DateTime Jetzt = new(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Tag = TimeSpan.FromHours(24);

        private static Appointment Termin(DateTime start, DateTime? gebucht, AppointmentStatus status = AppointmentStatus.Booked,
            DateTime? erinnert = null) => new()
        {
            Id = Guid.NewGuid(), Service = ServiceType.AllgemeineBeratung, Location = LocationType.BuergermtSued,
            StartUtc = start, EndUtc = start.AddMinutes(15), UserId = Guid.NewGuid(), Status = status,
            PostkorbHandle = "h", GebuchtUtc = gebucht, ErinnerungGesendetUtc = erinnert, RowVersion = [1],
        };

        [Fact]
        public async Task Nur_frueh_gebuchte_noch_nicht_erinnerte_Termine_im_Fenster_und_nur_einmal()
        {
            await using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var faellig = Termin(Jetzt.AddHours(20), Jetzt.AddDays(-3));
            db.Appointments.AddRange(
                faellig,
                Termin(Jetzt.AddHours(30), Jetzt.AddDays(-3)),                        // noch nicht im Fenster
                Termin(Jetzt.AddHours(20), Jetzt.AddHours(-1)),                       // kurzfristig gebucht
                Termin(Jetzt.AddHours(20), null),                                     // vor dieser Funktion gebucht
                Termin(Jetzt.AddHours(20), Jetzt.AddDays(-3), AppointmentStatus.Cancelled),
                Termin(Jetzt.AddHours(20), Jetzt.AddDays(-3), erinnert: Jetzt.AddHours(-2)),
                Termin(Jetzt.AddHours(-1), Jetzt.AddDays(-3)));                       // schon vorbei
            await db.SaveChangesAsync();
            var service = new TerminErinnerungService(new TerminErinnerungRepository(db), new ManualTime(Jetzt));

            var erinnerungen = await service.FaelligeAsync(Tag, CancellationToken.None);

            var erinnerung = Assert.Single(erinnerungen);
            Assert.Equal((faellig.Id, faellig.UserId, "h", LocationType.BuergermtSued), (erinnerung.TerminId, erinnerung.UserId,
                erinnerung.PostkorbHandle, erinnerung.Location));
            Assert.Equal(DateTimeKind.Utc, erinnerung.StartUtc.Kind);
            Assert.Empty(await service.FaelligeAsync(Tag, CancellationToken.None));
            Assert.Equal(Jetzt, (await db.Appointments.AsNoTracking().SingleAsync(a => a.Id == faellig.Id)).ErinnerungGesendetUtc);
        }
    }
}
