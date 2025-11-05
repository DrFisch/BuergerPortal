using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.Validations
{
    public sealed class ReisepassStep1Validator : AbstractValidator<ReisepassStep1Dto>
    {
        public ReisepassStep1Validator()
        {
            RuleFor(x => x.Vorname).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Nachname).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Geburtsdatum)
                .LessThan(DateOnly.FromDateTime(DateTime.UtcNow.Date));
            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                .MaximumLength(200);
            RuleFor(x => x.Telefon)
                .MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.Telefon));
        }
    }
}
