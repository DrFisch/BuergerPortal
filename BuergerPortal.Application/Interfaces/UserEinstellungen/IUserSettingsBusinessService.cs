using BuergerPortal.Application.Common;
using BuergerPortal.Application.UserEinstellungen.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Interfaces.UserEinstellungen
{
    public interface IUserSettingsBusinessService
    {
        Task<UserSettingsDto> GetForUserAsync(Guid userId, CancellationToken ct);
        Task<Result<bool>> UpsertForUserAsync(Guid userId, UserSettingsUpdateDto dto, CancellationToken ct);
    }
}
