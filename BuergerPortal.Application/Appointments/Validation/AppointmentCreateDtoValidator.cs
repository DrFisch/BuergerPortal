using BuergerPortal.Application.Appointments.DTOs;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace BuergerPortal.Application.Appointments.Validation
{
    public sealed class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
    {
        private readonly TimeZoneInfo _tz = TZConvert.GetTimeZoneInfo("Europe/Berlin");

        public AppointmentCreateDtoValidator()
        {
            RuleFor(x => x.StartUtc)
                .LessThan(x => x.EndUtc).WithMessage("Ende muss nach Start liegen.")
                .Must(BeNowOrFuture).WithMessage("Datum liegt in der Vergangenheit.");

            RuleFor(x => x).Custom((dto, ctx) =>
            {
                var startLocal = TimeZoneInfo.ConvertTimeFromUtc(dto.StartUtc, _tz);
                var endLocal = TimeZoneInfo.ConvertTimeFromUtc(dto.EndUtc, _tz);

                if (startLocal.Date != endLocal.Date)
                    ctx.AddFailure("Termin muss am selben Tag enden.");

                if (startLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    ctx.AddFailure("Nur Montag–Freitag 08:00–12:00.");

                var windowStart = startLocal.Date.AddHours(8);
                var windowEnd = startLocal.Date.AddHours(12);
                if (startLocal < windowStart || endLocal > windowEnd)
                    ctx.AddFailure("Termin liegt außerhalb der Geschäftszeit (08:00–12:00).");

                if (!IsQuarter(startLocal) || !IsQuarter(endLocal))
                    ctx.AddFailure("Start/Ende müssen auf :00/:15/:30/:45 liegen.");
            });
        }

        private static bool BeNowOrFuture(DateTime startUtc) => startUtc >= DateTime.UtcNow.AddMinutes(-1);
        private static bool IsQuarter(DateTime t) => t.Second == 0 && t.Millisecond == 0 && t.Minute % 15 == 0;
    }
}
