using BuergerPortal.Application.Antraege.AntragMeldebescheinigung.DTOs;
using FluentValidation;

namespace BuergerPortal.Application.Antraege.AntragMeldebescheinigung.Validations
{
    public sealed class MeldebescheinigungValidator : AbstractValidator<MeldebescheinigungDto>
    {
        public MeldebescheinigungValidator(TimeProvider time)
        {
            RuleFor(x => x.Vorname).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Nachname).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Geburtsdatum)
                .Must(d => d < DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
                .WithMessage("Das Geburtsdatum muss in der Vergangenheit liegen.");
            RuleFor(x => x.Geburtsort).MaximumLength(100);
            RuleFor(x => x.Geburtsname).MaximumLength(100);

            // Ohne Anschrift gibt es nichts zu bescheinigen (BundID-Konto ohne Adresse → Hinweis im Portal)
            RuleFor(x => x.Strasse).NotEmpty().WithMessage("Die Anschrift fehlt.").MaximumLength(200);
            RuleFor(x => x.PLZ).NotEmpty().WithMessage("Die Postleitzahl fehlt.").MaximumLength(10);
            RuleFor(x => x.Ort).NotEmpty().WithMessage("Der Ort fehlt.").MaximumLength(100);

            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
                .MaximumLength(200);
            RuleFor(x => x.Art).IsInEnum().WithMessage("Bitte die Art der Meldebescheinigung wählen.");
            RuleFor(x => x.Zweck)
                .MaximumLength(200).WithMessage("Der Zweck darf höchstens 200 Zeichen lang sein.");
        }
    }
}
