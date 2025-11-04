using BuergerPortal.Application.Antraege.AntragReisepass.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragReisepass.Validations
{
    public class ReisepassStep2Validator : AbstractValidator<ReisepassStep2Dto>
    {
        public ReisepassStep2Validator()
        {
            RuleFor(x => x.Hinweis).MaximumLength(1000);
        }
    }
}
