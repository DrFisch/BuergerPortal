using BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs;
using BuergerPortal.Application.Antraege.AntragSperrmuell.Mappings;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Enums;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.BusinessService
{
    public sealed class SperrmuellAntragBusinessService : ISperrmuellAntragBusinessService
    {
        private readonly ISperrmuellRepository _repo;
        private readonly IValidator<SperrmuellStep1Dto> _v1;
        private readonly IValidator<SperrmuellStep2Dto> _v2;
        public SperrmuellAntragBusinessService(ISperrmuellRepository repo, IValidator<SperrmuellStep1Dto> v1, IValidator<SperrmuellStep2Dto> v2)
        {
            _repo = repo;
            _v1 = v1;
            _v2 = v2;
        }
        public async Task<Result<Guid>> CreateStep1Async(SperrmuellStep1Dto dto, Guid currentUserId, CancellationToken ct)
        {
            var vr = await _v1.ValidateAsync(dto, ct);
            if (!vr.IsValid)
            {
                return Result<Guid>.Fail(
                    ErrorCodes.Validation,
                    string.Join("; ", vr.Errors.Select(e => e.ErrorMessage))
                );
            }

            var entity = SperrmuellMapper.ToNewEntity(dto, currentUserId);

            await _repo.AddAsync(entity, ct); // speichert selbst

            return Result<Guid>.Success(entity.Id);
        }

        public async Task<IReadOnlyList<SperrmuellSummaryDto>> GetAllForUserAsync(Guid currentUserId, CancellationToken ct)
        {
            var list = await _repo.GetAllForUserAsync(currentUserId, ct);

            return list
                .Select(SperrmuellMapper.ToSummaryDto)
                .ToList();
        }

        public async Task<Result<SperrmuellDetailDto>> GetAsync(Guid antragId, Guid currentUserId, CancellationToken ct)
        {
            var a = await _repo.GetAsync(antragId, ct);
            if (a is null)
                return Result<SperrmuellDetailDto>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");

            if (a.ApplicantUserId != currentUserId)
                return Result<SperrmuellDetailDto>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");

            var dto = SperrmuellMapper.ToDetailDto(a);

            return Result<SperrmuellDetailDto>.Success(dto);
        }

        public async Task<Result<bool>> SubmitAsync(Guid antragId, Guid currentUserId, CancellationToken ct)
        {
            var a = await _repo.GetAsync(antragId, ct);
            if (a is null)
                return Result<bool>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");

            if (a.ApplicantUserId != currentUserId)
                return Result<bool>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");

            if (a.Status is not (AntragStatus.Entwurf or AntragStatus.InPruefung))
            {
                return Result<bool>.Fail(
                    ErrorCodes.Validation,
                    $"Statuswechsel nicht erlaubt aus {a.Status}."
                );
            }

            a.Status = AntragStatus.Eingereicht;
            a.SubmittedUtc = DateTime.UtcNow;

            await _repo.UpdateAsync(a, ct);

            return Result<bool>.Success(true);
        }

        public async Task<Result<bool>> UpdateStep2Async(Guid antragId, SperrmuellStep2Dto dto, Guid currentUserId, CancellationToken ct)
        {
            var vr = await _v2.ValidateAsync(dto, ct);
            if (!vr.IsValid)
            {
                return Result<bool>.Fail(
                    ErrorCodes.Validation,
                    string.Join("; ", vr.Errors.Select(e => e.ErrorMessage))
                );
            }

            var a = await _repo.GetAsync(antragId, ct);
            if (a is null)
                return Result<bool>.Fail(ErrorCodes.NotFound, "Antrag nicht gefunden.");

            if (a.ApplicantUserId != currentUserId)
                return Result<bool>.Fail(ErrorCodes.Forbidden, "Zugriff verweigert.");

            // Mapping Step2 → Entity über Mapper
            SperrmuellMapper.ApplyStep2(a, dto);

            await _repo.UpdateAsync(a, ct); // speichert selbst

            return Result<bool>.Success(true);
        }
    }
}
