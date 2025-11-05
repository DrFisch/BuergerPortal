using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Application.Antraege.AntragReisepass.DTOs
{
    public record ReisepassSummaryDto(Guid Id,AntragStatus Status,DateTime CreatedUtc, DateTime? SubmittedUtc);
}
