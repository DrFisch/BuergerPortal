using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.Enums
{
    public enum AntragTyp 
    { 
        Reisepass=1,
        Sperrmuell=2,
        // seit 2026-10-18: Meldebescheinigung (Niveau "hoch", Zustellung ins BundID-Postfach)
        Meldebescheinigung=3
    }
}
