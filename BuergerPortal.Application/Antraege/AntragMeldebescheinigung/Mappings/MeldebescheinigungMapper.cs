using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.ValueObjects;

namespace BuergerPortal.Application.Antraege.AntragMeldebescheinigung.Mappings
{
    public static class MeldebescheinigungMapper
    {
        public static MeldebescheinigungAntrag ToNewEntity(MeldebescheinigungDto dto, Guid applicantUserId)
        {
            var a = new MeldebescheinigungAntrag { ApplicantUserId = applicantUserId };
            Apply(a, dto);
            return a;
        }

        public static void Apply(MeldebescheinigungAntrag a, MeldebescheinigungDto dto)
        {
            a.Name = new PersonName(dto.Vorname.Trim(), dto.Nachname.Trim());
            a.Birth = new PersonBirth(dto.Geburtsdatum);
            a.Geburtsort = Leer(dto.Geburtsort);
            a.Geburtsname = Leer(dto.Geburtsname);
            a.Addresse = new Addresse(dto.Strasse.Trim(), dto.PLZ.Trim(), dto.Ort.Trim());
            a.Kontakt = new Kontakt(Leer(dto.Email), null);
            a.Art = dto.Art;
            a.Zweck = Leer(dto.Zweck);
        }

        public static MeldebescheinigungDetailDto ToDetailDto(MeldebescheinigungAntrag a) => new(
            a.Id, a.Typ, a.Status, a.CreatedUtc, a.SubmittedUtc,
            a.Name.Vorname, a.Name.Nachname, a.Birth.Geburtsdatum, a.Geburtsort, a.Geburtsname,
            a.Addresse.Strasse, a.Addresse.PLZ, a.Addresse.Ort, a.Kontakt.Email, a.Art, a.Zweck);

        public static MeldebescheinigungSummaryDto ToSummaryDto(MeldebescheinigungAntrag a) => new(
            a.Id, a.Typ, a.Status, a.CreatedUtc, a.SubmittedUtc, a.Name.Vorname, a.Name.Nachname);

        private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
    }
}
