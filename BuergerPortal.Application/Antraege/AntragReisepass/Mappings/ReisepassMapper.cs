using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.Mappings
{
    public static class ReisepassMapper
    {
        public static ReisepassAntrag ToNewEntity(ReisepassStep1Dto dto, Guid applicantUserId)
            => new ReisepassAntrag
            {
                // Basisklasse Felder
                ApplicantUserId = applicantUserId,
                // Step 1
                Name = new PersonName(dto.Vorname, dto.Nachname),
                Birth = new PersonBirth(dto.Geburtsdatum),
                Kontakt = new Kontakt(dto.Email, dto.Telefon)
            };

        public static ReisepassDetailDto ToDetailDto(ReisepassAntrag a)
            => new(
                a.Id, a.Status, a.CreatedUtc, a.SubmittedUtc,
                a.Name.Vorname, a.Name.Nachname, a.Birth.Geburtsdatum,
                a.Kontakt.Email, a.Kontakt.Telefon,
                a.Express, a.AltpassVorhanden, a.Hinweis
            );

        public static ReisepassSummaryDto ToSummaryDto(ReisepassAntrag a)
            => new(a.Id, a.Status, a.CreatedUtc, a.SubmittedUtc);
    }
}
