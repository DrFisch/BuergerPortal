using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.DTOs
{
    public record ReisepassStep2Dto(bool Express, bool AltpassVorhanden, string? Hinweis);
}
