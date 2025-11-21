using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Antrag.ValueObjects
{
    public record Addresse(string Strasse, string PLZ, string Ort);
   
}
