using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.Entity
{
    public sealed class SperrmuellAntrag : Antrag
    {
        public SperrmuellAntrag()
        {
            Typ = AntragTyp.Sperrmuell;
        }

        public PersonName Name { get; set; }
        public PersonBirth Birth { get; set; }
        public Kontakt Kontakt { get; set; } = new(null, null);

        // Step 2 – Pass-Optionen
        public SperrmuellMengen Mengen { get; set; }
        public Addresse Addresse { get; set; }
        public DateTime Wunschzeit { get; set; }
        public string? Hinweis { get; set; }
    }
}
