using BuergerPortal.Api.Contracts.Appointments;
using BuergerPortal.Api.Controllers;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Mail;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Domain.Appointments.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>GET api/appointments/availability: was die Buchungsseite für einen Tag anzeigen darf.</summary>
    public class AvailabilityApiTests
    {
        private static readonly Guid UserId = Guid.NewGuid();

        // Do 08.10.2026: am Standort Mitte 09:00–09:30 vergeben, eigener Termin 10:00–10:15 in Nord
        private sealed class FakeAppointments : IAppointmentBusinessService
        {
            public LocationType? AskedLocation { get; private set; }

            public Task<List<BusySlotDto>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, LocationType? location, CancellationToken ct)
            {
                AskedLocation = location;
                return Task.FromResult(new List<BusySlotDto>
                {
                    new() { StartUtc = new DateTime(2026, 10, 8, 7, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 10, 8, 7, 30, 0, DateTimeKind.Utc) }
                });
            }

            public Task<List<AppointmentListItemDto>> GetAllForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(new List<AppointmentListItemDto>
            {
                new() { StartUtc = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 10, 8, 8, 15, 0, DateTimeKind.Utc), Location = LocationType.BuergermtNord },
                new() { StartUtc = new DateTime(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 10, 8, 9, 15, 0, DateTimeKind.Utc), Cancelled = true },
            });

            public Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> CancelAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<AppointmentListItemDto?> GetByIdAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> UpdateLocationAsync(Guid id, Guid currentUserId, LocationType newLocation, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class Unused : IEmailSender, IPostkorbService
        {
            public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default) =>
                Task.FromResult(PostkorbDeliveryStatus.Delivered);
        }

        private static (AppointmentsController Controller, FakeAppointments Service) Create()
        {
            var services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(new AppointmentValidatorTests.FixedTime(new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero)));
            var fake = new FakeAppointments();
            var unused = new Unused();
            var controller = new AppointmentsController(fake, unused, unused)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString())], "test")),
                        RequestServices = services.BuildServiceProvider(),
                    },
                },
            };
            return (controller, fake);
        }

        private static async Task<DayAvailabilityResponse> Get(string date, LocationType location = LocationType.BuergermtMitte)
        {
            var (controller, _) = Create();
            var result = await controller.GetAvailability(DateOnly.Parse(date), location, default);
            return Assert.IsType<DayAvailabilityResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        }

        [Fact]
        public async Task Werktag_liefert_vergebene_Zeiten_am_Standort_und_eigene_Termine_in_Ortszeit()
        {
            var (controller, fake) = Create();
            var result = await controller.GetAvailability(new DateOnly(2026, 10, 8), LocationType.BuergermtMitte, default);
            var day = Assert.IsType<DayAvailabilityResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

            Assert.Null(day.ClosedReason);
            Assert.Equal(LocationType.BuergermtMitte, fake.AskedLocation);
            Assert.Equal([new TimeRangeResponse("09:00", "09:30")], day.Taken);
            Assert.Equal([new TimeRangeResponse("10:00", "10:15")], day.Own); // stornierte zählen nicht
        }

        [Theory]
        [InlineData("2026-10-10", "Wochenende")]
        [InlineData("2026-12-25", "Feiertag (1. Weihnachtstag)")]
        [InlineData("2026-10-06", "Vergangenheit")]
        [InlineData("2027-01-07", "höchstens 90 Tage")]
        public async Task Geschlossene_Tage_haben_einen_Grund_und_keine_freien_Zeiten(string date, string grund)
        {
            var day = await Get(date);

            Assert.Contains(grund, day.ClosedReason);
            Assert.Empty(day.Taken);
        }
    }
}
