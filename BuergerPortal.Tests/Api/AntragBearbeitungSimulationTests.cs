using BuergerPortal.Api.Antraege;
using BuergerPortal.Application.Antraege.AntragMeldebescheinigung;
using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuergerPortal.Tests.Api
{
    /// <summary>
    /// Simulierte Antragsbearbeitung: Statusmeldung nach dem Vorbild von ZBP CreateState ins Postfach (echter API-Host,
    /// InMemory-Datenbank, Postfach nachgebildet) und die Texte der Meldung.
    /// </summary>
    public class AntragBearbeitungSimulationTests
    {
        private sealed class FakePostkorb : IPostkorbService
        {
            public List<PostkorbMessage> Sent { get; } = [];

            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default)
            {
                Sent.Add(message);
                return Task.FromResult(PostkorbDeliveryStatus.Delivered);
            }
        }

        private static readonly Guid Person = Guid.Parse("11111111-2222-3333-4444-555555555555");

        [Fact]
        public async Task Faelliger_Antrag_bekommt_Statusmeldung_ins_eigene_Postfach()
        {
            var postkorb = new FakePostkorb();
            using var factory = new ApiTestFactory(services: s =>
            {
                s.RemoveAll<IPostkorbService>();
                s.AddSingleton<IPostkorbService>(postkorb);
            });
            Guid id;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                var antrag = new ReisepassAntrag
                {
                    Id = Guid.NewGuid(),
                    ApplicantUserId = Person,
                    Name = new PersonName("Tina", "Test"),
                    Birth = new PersonBirth(new DateOnly(1990, 1, 1)),
                    Kontakt = new Kontakt(null, null),
                    RowVersion = [1],
                };
                db.ReisepassAntraege.Add(antrag);
                await db.SaveChangesAsync();
                id = antrag.Id;
                await scope.ServiceProvider.GetRequiredService<IAntragStatusRepository>().SetStatusAsync(id,
                    AntragStatus.Eingereicht, DateTime.UtcNow.AddMinutes(-3), "511fdf2b-70fc-3dd8-85a7-9ed9a9687910", CancellationToken.None);
            }

            var anzahl = await factory.Services.GetRequiredService<AntragBearbeitungSimulation>().RunOnceAsync(CancellationToken.None);

            Assert.Equal(1, anzahl);
            var nachricht = Assert.Single(postkorb.Sent);
            Assert.Equal("511fdf2b-70fc-3dd8-85a7-9ed9a9687910", nachricht.PostkorbHandle);
            Assert.Equal("Statusmeldung: Antrag auf einen Reisepass – in Bearbeitung", nachricht.Title);
            Assert.Contains($"Vorgangsnummer: {id}", nachricht.Content);
            Assert.Equal(3, nachricht.StorkQaaLevel);
            Assert.Equal(0, await factory.Services.GetRequiredService<AntragBearbeitungSimulation>().RunOnceAsync(CancellationToken.None));
        }

        [Fact]
        public void Sperrmuell_abgeschlossen_nennt_den_Wunschtag_Benachrichtigung_fuehrt_zum_Antrag()
        {
            var id = Guid.NewGuid();
            var wechsel = new AntragStatuswechsel(id, AntragTyp.Sperrmuell, AntragStatus.Genehmigt,
                new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc), Person, "h", "Tina Test", new DateTime(2026, 10, 20));

            var nachricht = AntragStatusmeldung.Nachricht(wechsel);
            var push = AntragStatusmeldung.Benachrichtigung(wechsel);

            Assert.Equal("Statusmeldung: Sperrmüll-Anmeldung – abgeschlossen", nachricht.Title);
            Assert.Contains("Guten Tag Tina Test,", nachricht.Content);
            Assert.Contains("Dienstag, 20.10.2026", nachricht.Content);
            Assert.Contains("simulierte Bearbeitung", nachricht.Content);
            Assert.DoesNotContain("\r", nachricht.Content);
            Assert.Equal(1, nachricht.StorkQaaLevel);
            Assert.Equal($"/Antraege/SperrmuellAntrag/{id}", push.Url);
            Assert.DoesNotContain("Tina", push.Title + push.Body);   // Sperrbildschirm: keine Namen
        }

        [Fact]
        public void Meldebescheinigung_abgeschlossen_enthaelt_die_Bescheinigung_lesbar_ab_hoch()
        {
            var id = Guid.NewGuid();
            var antrag = new MeldebescheinigungAntrag
            {
                Name = new PersonName("Tina", "Test"),
                Birth = new PersonBirth(new DateOnly(1990, 1, 1)),
                Geburtsort = "Fürth",
                Addresse = new Addresse("Rankestr. 13", "90461", "Nürnberg"),
                Art = MeldebescheinigungArt.Erweitert,
            };
            var wechsel = new AntragStatuswechsel(id, AntragTyp.Meldebescheinigung, AntragStatus.Genehmigt,
                new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc), Person, "h", "Tina Test", null,
                MeldebescheinigungDokument.Text(antrag));

            var nachricht = AntragStatusmeldung.Nachricht(wechsel);

            Assert.Equal("Statusmeldung: Meldebescheinigung – abgeschlossen", nachricht.Title);
            Assert.Equal(4, nachricht.StorkQaaLevel);
            Assert.Contains("ERWEITERTE MELDEBESCHEINIGUNG", nachricht.Content);
            Assert.Contains("MUSTER", nachricht.Content);
            Assert.Contains("Geburtsdatum: 01.01.1990", nachricht.Content);
            Assert.Contains("Geburtsort: Fürth", nachricht.Content);
            Assert.Contains("Anschrift (Hauptwohnung): Rankestr. 13, 90461 Nürnberg", nachricht.Content);
            Assert.Contains("Ausgestellt am: Montag, 19.10.2026, 10:00 Uhr", nachricht.Content);
            Assert.DoesNotContain("\r", nachricht.Content);
            Assert.Equal($"/Antraege/MeldebescheinigungAntrag/{id}", AntragStatusmeldung.Benachrichtigung(wechsel).Url);

            // einfache Bescheinigung: nur Name und Anschrift
            antrag.Art = MeldebescheinigungArt.Einfach;
            Assert.DoesNotContain("Geburtsdatum", MeldebescheinigungDokument.Text(antrag));
        }
    }
}
