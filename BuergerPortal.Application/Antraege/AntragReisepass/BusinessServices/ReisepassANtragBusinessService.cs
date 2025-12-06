using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Application.Antraege.AntragReisepass.Mappings;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.Enums;
using BuergerPortal.Domain.Antrag.ValueObjects;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.BusinessServices
{
    public sealed class ReisepassAntragBusinessService : IReisepassAntragBusinessService
    {
        private readonly IReisepassRepository _repo;
        private readonly IValidator<ReisepassStep1Dto> _v1;
        private readonly IValidator<ReisepassStep2Dto> _v2;

        public ReisepassAntragBusinessService(
            IReisepassRepository repo,
            IValidator<ReisepassStep1Dto> v1,
            IValidator<ReisepassStep2Dto> v2)
        { _repo = repo; _v1 = v1; _v2 = v2; }

        public async Task<Result<Guid>> CreateStep1Async(ReisepassStep1Dto dto, Guid userId, CancellationToken ct)
        {
            var vr = await _v1.ValidateAsync(dto, ct);
            if (!vr.IsValid) return Result<Guid>.Fail(ErrorCodes.Validation, string.Join("; ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = ReisepassMapper.ToNewEntity(dto, userId);
            await _repo.AddAsync(entity, ct); // speichert selbst
            return Result<Guid>.Success(entity.Id);
        }

        public async Task<Result<bool>> UpdateStep2Async(Guid id, ReisepassStep2Dto dto, Guid userId, CancellationToken ct)
        {
            var vr = await _v2.ValidateAsync(dto, ct);
            if (!vr.IsValid) return Result<bool>.Fail(ErrorCodes.Validation, string.Join("; ", vr.Errors.Select(e => e.ErrorMessage)));

            var a = await _repo.GetAsync(id, ct);
            if (a is null) return Result<bool>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");
            if (a.ApplicantUserId != userId) return Result<bool>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");

            a.Express = dto.Express;
            a.AltpassVorhanden = dto.AltpassVorhanden;
            a.Hinweis = string.IsNullOrWhiteSpace(dto.Hinweis) ? null : dto.Hinweis.Trim();

            await _repo.UpdateAsync(a, ct); // speichert selbst
            return Result<bool>.Success(true);
        }

        public async Task<Result<bool>> SubmitAsync(Guid id, Guid userId, CancellationToken ct)
        {
            var a = await _repo.GetAsync(id, ct);
            if (a is null) return Result<bool>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");
            if (a.ApplicantUserId != userId) return Result<bool>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");

            if (a.Status is not (AntragStatus.Entwurf or AntragStatus.InPruefung))
                return Result<bool>.Fail(ErrorCodes.Validation, $"Statuswechsel nicht erlaubt aus {a.Status}.");

            a.Status = AntragStatus.Eingereicht;
            a.SubmittedUtc = DateTime.UtcNow;

            await _repo.UpdateAsync(a, ct); // speichert selbst
            return Result<bool>.Success(true);
        }

        public async Task<Result<ReisepassDetailDto>> GetAsync(Guid id, Guid userId, CancellationToken ct)
        {
            var a = await _repo.GetAsync(id, ct);
            if (a is null) return Result<ReisepassDetailDto>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");
            if (a.ApplicantUserId != userId) return Result<ReisepassDetailDto>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");
            return Result<ReisepassDetailDto>.Success(ReisepassMapper.ToDetailDto(a));
        }

        public async Task<IReadOnlyList<ReisepassSummaryDto>> GetAllForUserAsync(Guid userId, CancellationToken ct)
        {
            var list = await _repo.GetAllForUserAsync(userId, ct);
            return list.Select(ReisepassMapper.ToSummaryDto).ToList();
        }
    }
}
