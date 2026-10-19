using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.Mappings;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using FluentValidation;

namespace BuergerPortal.Application.Antraege.AntragMeldebescheinigung.BusinessService
{
    /// <summary>
    /// Meldebescheinigung (2026-10-18): Entwurf in einem Schritt anlegen und ändern, einreichen. Das Niveau „hoch“
    /// prüft die API beim Einreichen (TrustLevelPolicies.High); fremde Anträge sind verboten, unbekannte nicht gefunden
    /// – wie bei Reisepass und Sperrmüll.
    /// </summary>
    public sealed class MeldebescheinigungAntragBusinessService(IMeldebescheinigungRepository repo,
        IValidator<MeldebescheinigungDto> validator, TimeProvider time) : IMeldebescheinigungAntragBusinessService
    {
        public async Task<Result<Guid>> CreateAsync(MeldebescheinigungDto dto, Guid currentUserId, CancellationToken ct)
        {
            var vr = await validator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return Result<Guid>.Fail(ErrorCodes.Validation, string.Join("; ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = MeldebescheinigungMapper.ToNewEntity(dto, currentUserId);
            await repo.AddAsync(entity, ct);
            return Result<Guid>.Success(entity.Id);
        }

        public async Task<Result<bool>> UpdateAsync(Guid antragId, MeldebescheinigungDto dto, Guid currentUserId,
            CancellationToken ct)
        {
            var vr = await validator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return Result<bool>.Fail(ErrorCodes.Validation, string.Join("; ", vr.Errors.Select(e => e.ErrorMessage)));

            var (a, fehler) = await EigenerAntragAsync<bool>(antragId, currentUserId, ct);
            if (a is null) return fehler;
            if (a.Status != AntragStatus.Entwurf)
                return Result<bool>.Fail(ErrorCodes.Validation, "Nur Entwürfe lassen sich ändern.");

            MeldebescheinigungMapper.Apply(a, dto);
            await repo.UpdateAsync(a, ct);
            return Result<bool>.Success(true);
        }

        public async Task<Result<bool>> SubmitAsync(Guid antragId, Guid currentUserId, CancellationToken ct)
        {
            var (a, fehler) = await EigenerAntragAsync<bool>(antragId, currentUserId, ct);
            if (a is null) return fehler;
            if (a.Status != AntragStatus.Entwurf)
                return Result<bool>.Fail(ErrorCodes.Validation, $"Statuswechsel nicht erlaubt aus {a.Status}.");

            a.Status = AntragStatus.Eingereicht;
            a.SubmittedUtc = time.GetUtcNow().UtcDateTime;
            await repo.UpdateAsync(a, ct);
            return Result<bool>.Success(true);
        }

        public async Task<Result<MeldebescheinigungDetailDto>> GetAsync(Guid antragId, Guid currentUserId,
            CancellationToken ct)
        {
            var (a, fehler) = await EigenerAntragAsync<MeldebescheinigungDetailDto>(antragId, currentUserId, ct);
            return a is null ? fehler : Result<MeldebescheinigungDetailDto>.Success(MeldebescheinigungMapper.ToDetailDto(a));
        }

        public async Task<IReadOnlyList<MeldebescheinigungSummaryDto>> GetAllForUserAsync(Guid currentUserId,
            CancellationToken ct) =>
            (await repo.GetAllForUserAsync(currentUserId, ct)).Select(MeldebescheinigungMapper.ToSummaryDto).ToList();

        private async Task<(MeldebescheinigungAntrag? Antrag, Result<T> Fehler)> EigenerAntragAsync<T>(Guid antragId,
            Guid currentUserId, CancellationToken ct)
        {
            var a = await repo.GetAsync(antragId, ct);
            if (a is null) return (null, Result<T>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden."));
            if (a.ApplicantUserId != currentUserId) return (null, Result<T>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert."));
            return (a, default);
        }
    }
}
