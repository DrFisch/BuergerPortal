using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.Enums
{
    public enum AntragStatus 
    { 
        Entwurf=1, 
        Eingereicht=2, 
        InPruefung=3, 
        Genehmigt=4, 
        Abgelehnt=5 
    }
}
