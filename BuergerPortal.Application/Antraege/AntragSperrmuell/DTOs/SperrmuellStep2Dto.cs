using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs
{
    public record SperrmuellStep2Dto(
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
