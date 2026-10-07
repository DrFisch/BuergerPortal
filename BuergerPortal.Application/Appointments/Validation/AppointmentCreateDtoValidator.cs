using BuergerPortal.Application.Appointments.Calendar;
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
    /// <summary>
    /// Fachliche Prüfung einer Terminbuchung: Geschäftszeiten der Bürgerämter (Mo–Fr 08:00–12:00 Ortszeit, nicht an
    /// Feiertagen in Bayern), 15-Minuten-Raster, erlaubte Dauern und höchstens <see cref="MaxDaysAhead"/> Tage im Voraus.
    /// </summary>
    public sealed class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
    {
        /// <summary>So viele Tage im Voraus sind Termine buchbar.</summary>
        public const int MaxDaysAhead = 90;

        /// <summary>Erlaubte Termindauern in Minuten (wie die Auswahl im Portal).</summary>
        public static readonly int[] AllowedDurations = [15, 30, 45];

        private readonly TimeZoneInfo _tz = TZConvert.GetTimeZoneInfo("Europe/Berlin");
        private readonly TimeProvider _time;

        public AppointmentCreateDtoValidator() : this(TimeProvider.System) { }

        public AppointmentCreateDtoValidator(TimeProvider time)
        {
            _time = time;

            RuleFor(x => x.StartUtc)
                .LessThan(x => x.EndUtc).WithMessage("Ende muss nach Start liegen.")
                .Must(BeNowOrFuture).WithMessage("Datum liegt in der Vergangenheit.");

            RuleFor(x => x).Custom((dto, ctx) =>
            {
                var startLocal = TimeZoneInfo.ConvertTimeFromUtc(dto.StartUtc, _tz);
                var endLocal = TimeZoneInfo.ConvertTimeFromUtc(dto.EndUtc, _tz);
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(_time.GetUtcNow().UtcDateTime, _tz).Date;

                if (startLocal.Date != endLocal.Date)
                    ctx.AddFailure("Termin muss am selben Tag enden.");

                if (startLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    ctx.AddFailure("Nur Montag–Freitag 08:00–12:00.");

                if (BayerischeFeiertage.Name(DateOnly.FromDateTime(startLocal)) is { } feiertag)
                    ctx.AddFailure($"Am {startLocal:dd.MM.yyyy} ist Feiertag ({feiertag}) – die Bürgerämter sind geschlossen.");

                if (startLocal.Date > todayLocal.AddDays(MaxDaysAhead))
                    ctx.AddFailure($"Termine können höchstens {MaxDaysAhead} Tage im Voraus gebucht werden.");

                var windowStart = startLocal.Date.AddHours(8);
                var windowEnd = startLocal.Date.AddHours(12);
                if (startLocal < windowStart || endLocal > windowEnd)
                    ctx.AddFailure("Termin liegt außerhalb der Geschäftszeit (08:00–12:00).");

                if (!IsQuarter(startLocal) || !IsQuarter(endLocal))
                    ctx.AddFailure("Start/Ende müssen auf :00/:15/:30/:45 liegen.");

                if (!AllowedDurations.Contains((int)(dto.EndUtc - dto.StartUtc).TotalMinutes))
                    ctx.AddFailure("Ein Termin dauert 15, 30 oder 45 Minuten.");
            });
        }

        private bool BeNowOrFuture(DateTime startUtc) => startUtc >= _time.GetUtcNow().UtcDateTime.AddMinutes(-1);
        private static bool IsQuarter(DateTime t) => t.Second == 0 && t.Millisecond == 0 && t.Minute % 15 == 0;
    }
}
