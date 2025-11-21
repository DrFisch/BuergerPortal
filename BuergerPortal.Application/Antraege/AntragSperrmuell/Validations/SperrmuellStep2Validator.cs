using BuergerPortal.Application.Antraege.AntragSperrmuell.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.Antraege.AntragSperrmuell.Validations
{
    public sealed class SperrmuellStep2Validator : AbstractValidator<SperrmuellStep2Dto>
    {
        public SperrmuellStep2Validator()
        {
            // Adresse
            RuleFor(x => x.Strasse)
                .NotEmpty().WithMessage("Die Straße darf nicht leer sein.")
                .MaximumLength(100);

            RuleFor(x => x.PLZ)
                .NotEmpty().WithMessage("Die Postleitzahl darf nicht leer sein.")
                .Length(5).WithMessage("Die Postleitzahl muss genau 5 Zeichen lang sein.");

            RuleFor(x => x.Ort)
                .NotEmpty().WithMessage("Der Ort darf nicht leer sein.")
                .MaximumLength(100);

            // Wunschzeit (Abholtermin)
            RuleFor(x => x.Wunschzeit)
                .Must(d => d > DateTime.UtcNow)
                .WithMessage("Der gewünschte Abholtermin muss in der Zukunft liegen.");

            // Müllmengen: >= 0 oder null
            RuleFor(x => x.HolzKubikmeter)
                .GreaterThanOrEqualTo(0)
                .When(x => x.HolzKubikmeter.HasValue)
                .WithMessage("Holzmenge muss 0 oder größer sein.");

            RuleFor(x => x.SonstigesKubikmeter)
                .GreaterThanOrEqualTo(0)
                .When(x => x.SonstigesKubikmeter.HasValue)
                .WithMessage("Menge sonstiger Materialien muss 0 oder größer sein.");

            RuleFor(x => x.Matratzen)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Matratzen.HasValue)
                .WithMessage("Anzahl der Matratzen muss 0 oder größer sein.");

            // Mindestens eine Angabe muss gemacht werden
            RuleFor(x => x)
                .Must(x =>
                    (x.HolzKubikmeter ?? 0) > 0 ||
                    (x.SonstigesKubikmeter ?? 0) > 0 ||
                    (x.Matratzen ?? 0) > 0
                )
                .WithMessage("Es muss mindestens eine Müllmenge angegeben werden.");

            // Hinweis ist optional, aber begrenzt
            RuleFor(x => x.Hinweis)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.Hinweis))
                .WithMessage("Der Hinweis darf maximal 500 Zeichen enthalten.");
        }
    }
}
