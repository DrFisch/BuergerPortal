using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.UserEinstellungen.DTOs
{
    public sealed class UserSettingsUpdateDto
    {
        public string Theme { get; init; } = "Light";
        public string Language { get; init; } = "de";
        public bool PushEnabled { get; init; }
        public bool ReduceDataUsage { get; init; }
        public bool AnalyticsOptIn { get; init; }
        public bool AllowGeolocation { get; init; }
        public byte[]? ExpectedVersion { get; init; }  // aus If-Match/body.Version
    }
}
