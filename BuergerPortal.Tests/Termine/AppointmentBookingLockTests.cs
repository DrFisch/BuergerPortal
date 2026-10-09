using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using BuergerPortal.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>
    /// Sperre beim Buchen gegen einen echten SQL Server (sp_getapplock): Gleicher Standort wartet, bis die erste Buchung
    /// fertig ist; ein anderer Standort wartet nicht. Verbindung aus BPSIM_TEST_SQL, sonst die eigene LocalDB-Test-DB
    /// BuergerPortalDB_bpsim. Ohne erreichbaren SQL Server (z. B. in der CI unter Linux) wird der Test übersprungen.
    /// Die Sperren ändern keine Daten.
    /// </summary>
    public class AppointmentBookingLockTests
    {
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("BPSIM_TEST_SQL")
            ?? @"Server=(localdb)\mssqllocaldb;Database=BuergerPortalDB_bpsim;Trusted_Connection=True;Connect Timeout=5";

        public sealed class SqlServerFactAttribute : FactAttribute
        {
            public SqlServerFactAttribute()
            {
                try
                {
                    using var connection = new SqlConnection(ConnectionString);
                    connection.Open();
                }
                catch (Exception ex)
                {
                    Skip = $"kein SQL Server erreichbar ({ex.GetType().Name})";
                }
            }
        }

        private static PortalDbContext NewContext() =>
            new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlServer(ConnectionString).Options);

        [SqlServerFact]
        public async Task Gleicher_Standort_wartet_bis_die_erste_Buchung_fertig_ist()
        {
            await using var dbA = NewContext();
            await using var dbB = NewContext();
            var first = await new SqlServerAppointmentBookingLock(dbA).AcquireAsync(Guid.NewGuid(), LocationType.BuergermtMitte, default);

            var second = new SqlServerAppointmentBookingLock(dbB).AcquireAsync(Guid.NewGuid(), LocationType.BuergermtMitte, default);
            await Task.Delay(700);
            Assert.False(second.IsCompleted, "Die zweite Buchung am selben Standort hätte warten müssen.");

            await first.DisposeAsync();   // erste Buchung fertig (hier ohne Speichern) → Sperre frei
            var lease = await second.WaitAsync(TimeSpan.FromSeconds(5));
            await lease.DisposeAsync();
        }

        [SqlServerFact]
        public async Task Anderer_Standort_wartet_nicht()
        {
            await using var dbA = NewContext();
            await using var dbB = NewContext();
            await using var first = await new SqlServerAppointmentBookingLock(dbA).AcquireAsync(Guid.NewGuid(), LocationType.BuergermtMitte, default);

            var other = new SqlServerAppointmentBookingLock(dbB).AcquireAsync(Guid.NewGuid(), LocationType.BuergermtNord, default);
            await using var lease = await other.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [SqlServerFact]
        public async Task Gleiche_Person_wartet_auch_an_anderem_Standort()
        {
            var person = Guid.NewGuid();
            await using var dbA = NewContext();
            await using var dbB = NewContext();
            var first = await new SqlServerAppointmentBookingLock(dbA).AcquireAsync(person, LocationType.BuergermtMitte, default);

            var second = new SqlServerAppointmentBookingLock(dbB).AcquireAsync(person, LocationType.BuergermtNord, default);
            await Task.Delay(700);
            Assert.False(second.IsCompleted, "Dieselbe Person hätte warten müssen (niemand ist an zwei Orten).");

            await first.CommitAsync(default);
            await first.DisposeAsync();
            await (await second.WaitAsync(TimeSpan.FromSeconds(5))).DisposeAsync();
        }
    }
}
