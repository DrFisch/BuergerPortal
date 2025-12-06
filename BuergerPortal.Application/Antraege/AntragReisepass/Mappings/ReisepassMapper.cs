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
                ApplicantUserId = applicantUserId,
                Name = new PersonName(dto.Vorname, dto.Nachname),
                Birth = new PersonBirth(dto.Geburtsdatum),
                Kontakt = new Kontakt(dto.Email, dto.Telefon)
            };

        public static ReisepassDetailDto ToDetailDto(ReisepassAntrag a)
            => new ReisepassDetailDto(
                Id: a.Id,
                Typ: a.Typ,               // <<< NEU: AntragTyp
                Status: a.Status,
                CreatedUtc: a.CreatedUtc,
                SubmittedUtc: a.SubmittedUtc,
                Vorname: a.Name.Vorname,
                Nachname: a.Name.Nachname,
                Geburtsdatum: a.Birth.Geburtsdatum,
                Email: a.Kontakt.Email,
                Telefon: a.Kontakt.Telefon,
                Express: a.Express,
                AltpassVorhanden: a.AltpassVorhanden,
                Hinweis: a.Hinweis
            );

        public static ReisepassSummaryDto ToSummaryDto(ReisepassAntrag a)
            => new ReisepassSummaryDto(
                Id: a.Id,
                Typ: a.Typ,               // <<< NEU: AntragTyp
                Status: a.Status,
                CreatedUtc: a.CreatedUtc,
                SubmittedUtc: a.SubmittedUtc
            );
    }
}
