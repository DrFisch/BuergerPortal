using BuergerPortal.Domain.Antrag.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs
{
    public record SperrmuellDetailDto(
        
    Guid Id,
    AntragTyp Typ,
    AntragStatus Status,
    DateTime CreatedUtc,
    DateTime? SubmittedUtc,
    string Vorname,
    string Nachname,
    DateOnly Geburtsdatum,
    string? Email,
    string? Telefon,
     string Strasse,
        string PLZ,
        string Ort,
        int? HolzKubikmeter,
        int? SonstigesKubikmeter,
        int? Matratzen,
        DateTime Wunschzeit,
        string? Hinweis
        );
}
