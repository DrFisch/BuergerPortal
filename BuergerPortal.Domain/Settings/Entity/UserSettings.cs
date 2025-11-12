using BuergerPortal.Domain.Settings.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Domain.Settings.Entity
{
    public class UserSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }             // FK auf deinen User (OIDC-Sub als Guid)
        public ThemeMode Theme { get; set; } = ThemeMode.Light;
        public string Language { get; set; } = "de";
        public bool PushEnabled { get; set; }
        public bool ReduceDataUsage { get; set; }
        public bool AnalyticsOptIn { get; set; }
        public bool AllowGeolocation { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = default!;
    }
}