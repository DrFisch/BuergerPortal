using BuergerPortal.Application.UserEinstellungen.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Application.UserEinstellungen.Validations
{
    public sealed class UserSettingsUpdateDtoValidator : AbstractValidator<UserSettingsUpdateDto>
    {
        private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase) { "Light", "Dark" };
        private static readonly HashSet<string> AllowedLangs = new(StringComparer.OrdinalIgnoreCase) { "de", "en" };

        public UserSettingsUpdateDtoValidator()
        {
            RuleFor(x => x.Theme).Must(t => AllowedThemes.Contains(t)).WithMessage("Ungültiges Theme.");
            RuleFor(x => x.Language).Must(l => AllowedLangs.Contains(l)).WithMessage("Ungültige Sprache.");
        }
    }
}
