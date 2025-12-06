using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs
{
    public record SperrmuellStep1Dto(string Vorname, string Nachname, DateOnly Geburtsdatum, string? Email, string? Telefon);
    
}
