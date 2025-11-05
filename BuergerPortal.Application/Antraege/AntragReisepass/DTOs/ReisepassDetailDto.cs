using BuergerPortal.Domain.Antrag.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.DTOs
{
    public sealed record ReisepassDetailDto(
    Guid Id,
    AntragStatus Status,
    DateTime CreatedUtc,
    DateTime? SubmittedUtc,
    string Vorname,
    string Nachname,
    DateOnly Geburtsdatum,
    string? Email,
    string? Telefon,
    bool Express,
    bool AltpassVorhanden,
    string? Hinweis);
}
