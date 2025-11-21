using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.ValueObjects
{
    public record SperrmuellMengen(
    int? HolzKubikmeter,
    int? SonstigesKubikmeter,
    int? Matratzen    
);
}
