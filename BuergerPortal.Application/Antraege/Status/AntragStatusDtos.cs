using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Application.Antraege.Status
{
    /// <summary>Ein Eintrag der Zeitleiste eines Antrags.</summary>
    public sealed record AntragStatusEreignisDto(AntragStatus Status, DateTime ZeitpunktUtc);

    /// <summary>
    /// Ein Statuswechsel der simulierten Bearbeitung mit allem, was die Statusmeldung ins BundID-Postfach und die
    /// Benachrichtigung brauchen (Person, Handle, Name für die Anrede, bei Sperrmüll der Wunschtermin).
    /// </summary>
    public sealed record AntragStatuswechsel(Guid AntragId, AntragTyp Typ, AntragStatus Status, DateTime ZeitpunktUtc,
        Guid ApplicantUserId, string? PostkorbHandle, string? Name, DateTime? WunschzeitUtc);
}
