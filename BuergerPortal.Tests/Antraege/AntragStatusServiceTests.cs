using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Infrastructure.Repositories;
using static BuergerPortal.Tests.Antraege.AntragStatusRepositoryTests;

namespace BuergerPortal.Tests.Antraege
{
    /// <summary>Zeitleiste und simulierte Bearbeitung (eingereicht → in Bearbeitung → genehmigt) mit einstellbarer Uhr.</summary>
    public class AntragStatusServiceTests
    {
        private sealed class ManualTime(DateTimeOffset now) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = now;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static readonly TimeSpan Zwei = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan Fuenf = TimeSpan.FromMinutes(5);

        [Fact]
        public async Task Bearbeitung_laeuft_in_zwei_Schritten_mit_den_Angaben_fuer_die_Statusmeldung()
        {
            await using var db = NewDb();
            var antrag = await SeedAsync(db);
            var time = new ManualTime(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero));
            var service = new AntragStatusService(new AntragStatusRepository(db), time);
            await service.EingereichtAsync(antrag.Id, "handle-1", CancellationToken.None);

            time.Now = time.Now.AddMinutes(1);
            Assert.Empty(await service.BearbeitungSimulierenAsync(Zwei, Fuenf, CancellationToken.None));

            time.Now = time.Now.AddMinutes(2);
            var erster = Assert.Single(await service.BearbeitungSimulierenAsync(Zwei, Fuenf, CancellationToken.None));
            Assert.Equal((AntragStatus.InPruefung, AntragTyp.Reisepass, "handle-1", "Tina Test", Person),
                (erster.Status, erster.Typ, erster.PostkorbHandle, erster.Name, erster.ApplicantUserId));

            time.Now = time.Now.AddMinutes(5);
            var zweiter = Assert.Single(await service.BearbeitungSimulierenAsync(Zwei, Fuenf, CancellationToken.None));
            Assert.Equal(AntragStatus.Genehmigt, zweiter.Status);

            time.Now = time.Now.AddMinutes(30);
            Assert.Empty(await service.BearbeitungSimulierenAsync(Zwei, Fuenf, CancellationToken.None));
        }

        [Fact]
        public async Task Zeitleiste_nur_fuer_die_eigene_Person_in_zeitlicher_Reihenfolge()
        {
            await using var db = NewDb();
            var antrag = await SeedAsync(db);
            var time = new ManualTime(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero));
            var service = new AntragStatusService(new AntragStatusRepository(db), time);
            await service.EingereichtAsync(antrag.Id, null, CancellationToken.None);
            time.Now = time.Now.AddMinutes(3);
            await service.BearbeitungSimulierenAsync(Zwei, Fuenf, CancellationToken.None);

            var verlauf = (await service.GetVerlaufAsync(antrag.Id, Person, CancellationToken.None))!;

            Assert.Equal([AntragStatus.Eingereicht, AntragStatus.InPruefung], verlauf.Select(e => e.Status));
            Assert.All(verlauf, e => Assert.Equal(DateTimeKind.Utc, e.ZeitpunktUtc.Kind));
            Assert.Null(await service.GetVerlaufAsync(antrag.Id, Guid.NewGuid(), CancellationToken.None));
        }
    }
}
