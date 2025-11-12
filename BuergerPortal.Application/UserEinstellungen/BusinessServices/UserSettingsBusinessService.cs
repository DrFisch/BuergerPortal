using BuergerPortal.Application.Common;
using BuergerPortal.Domain.Settings.Enums;
using FluentValidation;
using BuergerPortal.Domain.Settings.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuergerPortal.Application.Interfaces.UserEinstellungen;
using BuergerPortal.Application.UserEinstellungen.DTOs;

namespace BuergerPortal.Application.UserEInstellungen.BusinessServices
{
    public sealed class UserSettingsBusinessService : IUserSettingsBusinessService
    {
        private readonly IUserSettingsRepository _repo;
        private readonly IValidator<UserSettingsUpdateDto> _validator;

        public UserSettingsBusinessService(IUserSettingsRepository repo, IValidator<UserSettingsUpdateDto> validator)
        {
            _repo = repo;
            _validator = validator;
        }

        public async Task<UserSettingsDto> GetForUserAsync(Guid userId, CancellationToken ct)
        {
            var entity = await _repo.GetByUserIdAsync(userId, ct);
            if (entity is null)
            {
                // noch nichts gespeichert → Defaults nur anzeigen (kein Insert)
                return new UserSettingsDto { Theme = "Light", Language = "de" };
            }

            return Map(entity);
        }

        public async Task<Result<bool>> UpsertForUserAsync(Guid userId, UserSettingsUpdateDto dto, CancellationToken ct)
        {
            if (userId == Guid.Empty)
                return Result<bool>.Fail(ErrorCodes.Validation, "Ungültige Benutzer-ID.");

            var vr = await _validator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
            {
                var msg = string.Join(" ", vr.Errors.Select(e => e.ErrorMessage).Distinct());
                return Result<bool>.Fail(ErrorCodes.Validation, msg);
            }

            var existing = await _repo.GetByUserIdAsync(userId, ct);

            if (existing is null)
            {
                var entity = new UserSettings
                {
                    UserId = userId,
                    Theme = dto.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? ThemeMode.Dark : ThemeMode.Light,
                    Language = string.IsNullOrWhiteSpace(dto.Language) ? "de" : dto.Language,
                    PushEnabled = dto.PushEnabled,
                    ReduceDataUsage = dto.ReduceDataUsage,
                    AnalyticsOptIn = dto.AnalyticsOptIn,
                    AllowGeolocation = dto.AllowGeolocation,
                    UpdatedUtc = DateTime.UtcNow
                };
                await _repo.CreateAsync(entity, ct);
                return Result<bool>.Success(true);
            }

            // Concurrency prüfen, falls ExpectedVersion mitgeliefert
            if (dto.ExpectedVersion is not null && !existing.RowVersion.SequenceEqual(dto.ExpectedVersion))
                return Result<bool>.Fail(ErrorCodes.Concurrency, "Die Einstellungen wurden inzwischen geändert.");

            existing.Theme = dto.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? ThemeMode.Dark : ThemeMode.Light;
            existing.Language = string.IsNullOrWhiteSpace(dto.Language) ? "de" : dto.Language;
            existing.PushEnabled = dto.PushEnabled;
            existing.ReduceDataUsage = dto.ReduceDataUsage;
            existing.AnalyticsOptIn = dto.AnalyticsOptIn;
            existing.AllowGeolocation = dto.AllowGeolocation;
            existing.UpdatedUtc = DateTime.UtcNow;

            await _repo.UpdateAsync(existing, ct);
            return Result<bool>.Success(true);
        }

        private static UserSettingsDto Map(UserSettings e) => new()
        {
            Theme = e.Theme == ThemeMode.Dark ? "Dark" : "Light",
            Language = e.Language,
            PushEnabled = e.PushEnabled,
            ReduceDataUsage = e.ReduceDataUsage,
            AnalyticsOptIn = e.AnalyticsOptIn,
            AllowGeolocation = e.AllowGeolocation,
            Version = Convert.ToBase64String(e.RowVersion ?? Array.Empty<byte>())
        };
    }
}
