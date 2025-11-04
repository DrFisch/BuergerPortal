using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Antrag.Entity;
using BuergerPortal.Domain.Antrag.ValueObjects;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.BusinessServices
{
    public class ReisepassAntragService
    {
        //private readonly IAntragRepository _repo;
        //private readonly IUnitOfWork _uow;
        //private readonly ICurrentUser _user;
        //private readonly IDateTimeProvider _time;
        //private readonly IValidator<ReisepassStep1Dto> _step1Validator;
        //private readonly IValidator<ReisepassStep2Dto> _step2Validator;

        //public ReisepassAntragService(
        //    IAntragRepository repo,
        //    IUnitOfWork uow,
        //    ICurrentUser user,
        //    IDateTimeProvider time,
        //    IValidator<ReisepassStep1Dto> step1Validator,
        //    IValidator<ReisepassStep2Dto> step2Validator)
        //{
        //    _repo = repo; _uow = uow; _user = user; _time = time;
        //    _step1Validator = step1Validator; _step2Validator = step2Validator;
        //}

        //public async Task<Guid> CreateStep1Async(ReisepassStep1Dto dto, CancellationToken ct = default)
        //{
        //    await _step1Validator.ValidateAndThrowAsync(dto, ct);

        //    if (string.IsNullOrWhiteSpace(_user.UserId))
        //        throw new InvalidOperationException("User not authenticated.");

        //    var entity = new ReisepassAntrag();
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.ApplicantUserId))!
        //          .SetValue(entity, _user.UserId);
        //    // Domain-Methoden nutzen, falls vorhanden:
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Name))!
        //          .SetValue(entity, new PersonName(dto.Vorname, dto.Nachname));
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Birth))!
        //          .SetValue(entity, new PersonBirth(dto.Geburtsdatum));
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Kontakt))!
        //          .SetValue(entity, new Kontakt(dto.Email, dto.Telefon));

        //    await _repo.AddReisepassAsync(entity, ct);
        //    await _uow.SaveChangesAsync(ct);
        //    return entity.Id;
        //}

        //public async Task UpdateStep2Async(Guid antragId, ReisepassStep2Dto dto, CancellationToken ct = default)
        //{
        //    await _step2Validator.ValidateAndThrowAsync(dto, ct);

        //    var entity = await _repo.GetReisepassAntragByIdAsync(antragId, ct)
        //              ?? throw new KeyNotFoundException("Antrag nicht gefunden.");

        //    if (entity.ApplicantUserId != _user.UserId)
        //        throw new UnauthorizedAccessException();

        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Express))!.SetValue(entity, dto.Express);
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.AltpassVorhanden))!.SetValue(entity, dto.AltpassVorhanden);
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Hinweis))!.SetValue(entity, string.IsNullOrWhiteSpace(dto.Hinweis) ? null : dto.Hinweis.Trim());

        //    await _repo.UpdateAsync(entity, ct);
        //    await _uow.SaveChangesAsync(ct);
        //}

        //public async Task SubmitAsync(Guid antragId, CancellationToken ct = default)
        //{
        //    var entity = await _repo.GetReisepassAntragByIdAsync(antragId, ct)
        //              ?? throw new KeyNotFoundException("Antrag nicht gefunden.");

        //    if (entity.ApplicantUserId != _user.UserId)
        //        throw new UnauthorizedAccessException();

        //    // Domain-Invariante: Statuswechsel
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.Status))!.SetValue(entity, Domain.Antrag.Enums.AntragStatus.Eingereicht);
        //    entity.GetType().GetProperty(nameof(ReisepassAntrag.SubmittedUtc))!.SetValue(entity, _time.UtcNow);

        //    await _repo.UpdateAsync(entity, ct);
        //    await _uow.SaveChangesAsync(ct);
        //}
    }
}
