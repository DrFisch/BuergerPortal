using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs
{
    /// <summary>
    /// Anlegen und Ändern eines Entwurfs (ein Schritt): Personendaten und Anschrift aus der BundID-Anmeldung, dazu Art,
    /// freiwilliger Zweck und Kontakt.
    /// </summary>
    public sealed record MeldebescheinigungDto(
        string Vorname,
        string Nachname,
        DateOnly Geburtsdatum,
        string? Geburtsort,
        string? Geburtsname,
        string Strasse,
        string PLZ,
        string Ort,
        string? Email,
        MeldebescheinigungArt Art,
        string? Zweck);

    public sealed record MeldebescheinigungDetailDto(
        Guid Id,
        AntragTyp Typ,
        AntragStatus Status,
        DateTime CreatedUtc,
        DateTime? SubmittedUtc,
        string Vorname,
        string Nachname,
        DateOnly Geburtsdatum,
        string? Geburtsort,
        string? Geburtsname,
        string Strasse,
        string PLZ,
        string Ort,
        string? Email,
        MeldebescheinigungArt Art,
        string? Zweck);

    public sealed record MeldebescheinigungSummaryDto(Guid Id, AntragTyp Typ, AntragStatus Status, DateTime CreatedUtc,
        DateTime? SubmittedUtc, string Vorname, string Nachname);
}
