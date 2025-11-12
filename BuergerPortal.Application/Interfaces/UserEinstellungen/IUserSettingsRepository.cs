using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuergerPortal.Domain.Settings.Entity;

namespace BuergerPortal.Application.Interfaces.UserEinstellungen
{
    public interface IUserSettingsRepository
    {
        Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct);
        Task CreateAsync(UserSettings entity, CancellationToken ct);
        Task UpdateAsync(UserSettings entity, CancellationToken ct);
    }
}
