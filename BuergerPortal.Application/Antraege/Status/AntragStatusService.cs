using BuergerPortal.Application.Antraege.AntragMeldebescheinigung;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Application.Antraege.Status
{
    public sealed class AntragStatusService(IAntragStatusRepository repo, TimeProvider time) : IAntragStatusService
    {
        public Task EingereichtAsync(Guid antragId, string? postkorbHandle, CancellationToken ct) =>
            repo.SetStatusAsync(antragId, AntragStatus.Eingereicht, time.GetUtcNow().UtcDateTime, postkorbHandle, ct);

        public async Task<IReadOnlyList<AntragStatusEreignisDto>?> GetVerlaufAsync(Guid antragId, Guid userId, CancellationToken ct)
        {
            if (!await repo.GehoertPersonAsync(antragId, userId, ct))
            {
                return null;
            }
            var verlauf = await repo.GetVerlaufAsync(antragId, ct);
            return verlauf
                .Select(e => new AntragStatusEreignisDto(e.Status, DateTime.SpecifyKind(e.ZeitpunktUtc, DateTimeKind.Utc)))
                .ToList();
        }

        public async Task<IReadOnlyList<AntragStatuswechsel>> BearbeitungSimulierenAsync(TimeSpan bisInBearbeitung,
            TimeSpan bisAbschluss, CancellationToken ct)
        {
            var jetzt = time.GetUtcNow().UtcDateTime;
            var wechsel = new List<AntragStatuswechsel>();
            foreach (var (von, nach, warten) in new[]
                     {
                         (AntragStatus.Eingereicht, AntragStatus.InPruefung, bisInBearbeitung),
                         (AntragStatus.InPruefung, AntragStatus.Genehmigt, bisAbschluss),
                     })
            {
                foreach (var antrag in await repo.GetFaelligeAsync(von, jetzt - warten, ct))
                {
                    if (await repo.SetStatusAsync(antrag.Id, nach, jetzt, null, ct))
                    {
                        wechsel.Add(new AntragStatuswechsel(antrag.Id, antrag.Typ, nach, jetzt, antrag.ApplicantUserId,
                            antrag.PostkorbHandle, Name(antrag), (antrag as SperrmuellAntrag)?.Wunschzeit,
                            Dokument(antrag, nach)));
                    }
                }
            }
            return wechsel;
        }

        public Task<EntwurfVerwerfenErgebnis> EntwurfVerwerfenAsync(Guid antragId, Guid userId, CancellationToken ct) =>
            repo.EntwurfLoeschenAsync(antragId, userId, ct);

        private static string? Name(Antrag antrag) => antrag switch
        {
            ReisepassAntrag r => $"{r.Name.Vorname} {r.Name.Nachname}",
            SperrmuellAntrag s => $"{s.Name.Vorname} {s.Name.Nachname}",
            MeldebescheinigungAntrag m => $"{m.Name.Vorname} {m.Name.Nachname}",
            _ => null,
        };

        // Die Meldebescheinigung selbst geht mit der Meldung "abgeschlossen" ins Postfach (es kennt keine Anhänge).
        private static string? Dokument(Antrag antrag, AntragStatus nach) =>
            antrag is MeldebescheinigungAntrag m && nach == AntragStatus.Genehmigt ? MeldebescheinigungDokument.Text(m) : null;
    }
}
