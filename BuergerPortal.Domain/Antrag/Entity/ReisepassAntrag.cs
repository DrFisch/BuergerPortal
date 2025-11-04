using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.Entity
{
    public sealed class ReisepassAntrag : Antrag
    {
        public ReisepassAntrag()
        {
            Typ = AntragTyp.Reisepass;
        }

        // Step 1 – Person
        public PersonName Name { get; set; }
        public PersonBirth Birth { get; set; } 
        public Kontakt Kontakt { get; set; } = new(null, null);

        // Step 2 – Pass-Optionen
        public bool Express { get; set; }
        public bool AltpassVorhanden { get; set; }
        public string? Hinweis { get; set; }
    }
}
