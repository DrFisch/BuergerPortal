using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using BuergerPortal.Infrastructure.Persistence;
using BuergerPortal.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Tests.Antraege
{
    /// <summary>Statusverlauf: Wechsel festhalten, Verlauf lesen, fällige Anträge für die simulierte Bearbeitung finden.</summary>
    public class AntragStatusRepositoryTests
    {
        internal static readonly Guid Person = Guid.Parse("11111111-2222-3333-4444-555555555555");

        internal static PortalDbContext NewDb() =>
            new(new DbContextOptionsBuilder<PortalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        internal static async Task<ReisepassAntrag> SeedAsync(PortalDbContext db, AntragStatus status = AntragStatus.Entwurf)
        {
            var antrag = new ReisepassAntrag
            {
                Id = Guid.NewGuid(),
                ApplicantUserId = Person,
                Status = status,
                Name = new PersonName("Tina", "Test"),
                Birth = new PersonBirth(new DateOnly(1990, 1, 1)),
                Kontakt = new Kontakt(null, null),
                RowVersion = [1],
            };
            db.ReisepassAntraege.Add(antrag);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return antrag;
        }

        [Fact]
        public async Task Statuswechsel_setzt_Status_haelt_Ereignis_und_Handle_fest()
        {
            await using var db = NewDb();
            var antrag = await SeedAsync(db);
            var repo = new AntragStatusRepository(db);
            var zeit = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);

            Assert.True(await repo.SetStatusAsync(antrag.Id, AntragStatus.Eingereicht, zeit, "handle-1", CancellationToken.None));

            var gespeichert = await db.Antraege.AsNoTracking().SingleAsync(a => a.Id == antrag.Id);
            Assert.Equal(AntragStatus.Eingereicht, gespeichert.Status);
            Assert.Equal("handle-1", gespeichert.PostkorbHandle);
            var ereignis = Assert.Single(await repo.GetVerlaufAsync(antrag.Id, CancellationToken.None));
            Assert.Equal((AntragStatus.Eingereicht, zeit), (ereignis.Status, ereignis.ZeitpunktUtc));
        }

        [Fact]
        public async Task Ohne_Handle_bleibt_das_gespeicherte_Handle_erhalten_unbekannter_Antrag_false()
        {
            await using var db = NewDb();
            var antrag = await SeedAsync(db);
            var repo = new AntragStatusRepository(db);
            await repo.SetStatusAsync(antrag.Id, AntragStatus.Eingereicht, DateTime.UtcNow, "handle-1", CancellationToken.None);

            await repo.SetStatusAsync(antrag.Id, AntragStatus.InPruefung, DateTime.UtcNow, null, CancellationToken.None);

            Assert.Equal("handle-1", (await db.Antraege.AsNoTracking().SingleAsync()).PostkorbHandle);
            Assert.False(await repo.SetStatusAsync(Guid.NewGuid(), AntragStatus.InPruefung, DateTime.UtcNow, null, CancellationToken.None));
        }

        [Fact]
        public async Task Faellig_ist_nur_wer_lange_genug_im_Status_ist_und_einen_Verlauf_hat()
        {
            await using var db = NewDb();
            var repo = new AntragStatusRepository(db);
            var jetzt = new DateTime(2026, 10, 11, 8, 10, 0, DateTimeKind.Utc);
            var alt = await SeedAsync(db);
            var neu = await SeedAsync(db);
            await SeedAsync(db, AntragStatus.Eingereicht);   // eingereicht vor dieser Funktion: kein Verlauf
            await repo.SetStatusAsync(alt.Id, AntragStatus.Eingereicht, jetzt.AddMinutes(-3), null, CancellationToken.None);
            await repo.SetStatusAsync(neu.Id, AntragStatus.Eingereicht, jetzt.AddMinutes(-1), null, CancellationToken.None);

            var faellig = await repo.GetFaelligeAsync(AntragStatus.Eingereicht, jetzt.AddMinutes(-2), CancellationToken.None);

            Assert.Equal([alt.Id], faellig.Select(a => a.Id));
            Assert.IsType<ReisepassAntrag>(faellig[0]);
        }

        [Fact]
        public async Task Antrag_gehoert_nur_der_eigenen_Person()
        {
            await using var db = NewDb();
            var antrag = await SeedAsync(db);
            var repo = new AntragStatusRepository(db);

            Assert.True(await repo.GehoertPersonAsync(antrag.Id, Person, CancellationToken.None));
            Assert.False(await repo.GehoertPersonAsync(antrag.Id, Guid.NewGuid(), CancellationToken.None));
        }
    }
}
