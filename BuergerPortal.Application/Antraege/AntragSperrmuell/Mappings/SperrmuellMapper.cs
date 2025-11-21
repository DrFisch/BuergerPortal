using BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.Mappings
{
    public static class SperrmuellMapper
    {
        // ------------------------------------------------------------
        // Step 1: Neue Entity erzeugen
        // ------------------------------------------------------------
        public static SperrmuellAntrag ToNewEntity(SperrmuellStep1Dto dto, Guid applicantUserId)
        => new SperrmuellAntrag
        {
            ApplicantUserId = applicantUserId,
            Name = new PersonName(dto.Vorname, dto.Nachname),
            Birth = new PersonBirth(dto.Geburtsdatum),
            Kontakt = new Kontakt(dto.Email, dto.Telefon),

            // Dummy-Adresse für Step 1 (wird in Step 2 überschrieben)
            Addresse = new Addresse(
                Strasse: string.Empty,
                PLZ: string.Empty,
                Ort: string.Empty
            ),

            // leere Mengen
            Mengen = new SperrmuellMengen(
                HolzKubikmeter: null,
                SonstigesKubikmeter: null,
                Matratzen: null
            ),

            // irgendein Default für Wunschzeit (wird in Step 2 gesetzt)
            Wunschzeit = DateTime.UtcNow
        };

        // ------------------------------------------------------------
        // Step 2: Werte auf bestehende Entity anwenden
        // ------------------------------------------------------------
        public static void ApplyStep2(SperrmuellAntrag a, SperrmuellStep2Dto dto)
        {
            a.Addresse = new Addresse(
                dto.Strasse,
                dto.PLZ,
                dto.Ort
            );

            a.Mengen = new SperrmuellMengen(
                dto.HolzKubikmeter,
                dto.SonstigesKubikmeter,
                dto.Matratzen
            );

            a.Wunschzeit = dto.Wunschzeit;
            a.Hinweis = string.IsNullOrWhiteSpace(dto.Hinweis)
                ? null
                : dto.Hinweis.Trim();
        }

        // ------------------------------------------------------------
        // Entity → DetailDto
        // ------------------------------------------------------------
        public static SperrmuellDetailDto ToDetailDto(SperrmuellAntrag a)
            => new SperrmuellDetailDto(
                Id: a.Id,
                Typ: a.Typ,
                Status: a.Status,
                CreatedUtc: a.CreatedUtc,
                SubmittedUtc: a.SubmittedUtc,

                Vorname: a.Name.Vorname,
                Nachname: a.Name.Nachname,
                Geburtsdatum: a.Birth.Geburtsdatum,

                Email: a.Kontakt.Email,
                Telefon: a.Kontakt.Telefon,

                Strasse: a.Addresse.Strasse,
                PLZ: a.Addresse.PLZ,
                Ort: a.Addresse.Ort,

                HolzKubikmeter: a.Mengen.HolzKubikmeter,
                SonstigesKubikmeter: a.Mengen.SonstigesKubikmeter,
                Matratzen: a.Mengen.Matratzen,

                Wunschzeit: a.Wunschzeit,
                Hinweis: a.Hinweis
            );

        // ------------------------------------------------------------
        // Entity → SummaryDto
        // ------------------------------------------------------------
        public static SperrmuellSummaryDto ToSummaryDto(SperrmuellAntrag a)
            => new SperrmuellSummaryDto(
                Id: a.Id,
                Typ: a.Typ,
                Status: a.Status,
                CreatedUtc: a.CreatedUtc,
                SubmittedUtc: a.SubmittedUtc
            );
    }
}
