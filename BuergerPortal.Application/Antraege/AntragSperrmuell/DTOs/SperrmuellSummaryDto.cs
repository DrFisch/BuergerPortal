using BuergerPortal.Domain.Antrag.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs
{
    public record SperrmuellSummaryDto(Guid Id, AntragTyp Typ, AntragStatus Status, DateTime CreatedUtc, DateTime? SubmittedUtc);
    
}
