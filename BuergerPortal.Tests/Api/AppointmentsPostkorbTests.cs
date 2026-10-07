using BuergerPortal.Api.Contracts.Appointments;
using BuergerPortal.Api.Controllers;
using BuergerPortal.Api.Postkorb;
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

namespace BuergerPortal.Tests.Api
{
    /// <summary>Bestätigungen bei der Terminbuchung: nur bei Erfolg, Ergebnis der Zustellung im Header.</summary>
    public class AppointmentsPostkorbTests
    {
        private const string Handle = "11b2dc8f-3831-3b26-afde-aa0be42bd79b";

        private sealed class FakePostkorb : IPostkorbService
        {
            public List<PostkorbMessage> Sent { get; } = [];
            public PostkorbDeliveryStatus Status { get; set; } = PostkorbDeliveryStatus.Delivered;

            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default)
            {
                Sent.Add(message);
                return Task.FromResult(Status);
            }
        }

        private sealed class FakeEmail : IEmailSender
        {
            public int Count { get; private set; }

            public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
            {
                Count++;
                return Task.CompletedTask;
            }
        }

        // Nur BookAsync wird gebraucht.
        private sealed class FakeAppointments(Result<Guid> bookResult) : IAppointmentBusinessService
        {
            public Task<Result<Guid>> BookAsync(AppointmentCreateDto dto, Guid currentUserId, CancellationToken ct) =>
                Task.FromResult(bookResult);
            public Task<List<AppointmentListItemDto>> GetAllForUserAsync(Guid userId, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BusySlotDto>> GetBusyAsync(DateTime fromUtc, DateTime toUtc, LocationType? location, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> CancelAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<AppointmentListItemDto?> GetByIdAsync(Guid id, Guid currentUserId, CancellationToken ct) => throw new NotImplementedException();
            public Task<Result<Guid>> UpdateLocationAsync(Guid id, Guid currentUserId, LocationType newLocation, CancellationToken ct) => throw new NotImplementedException();
        }

        private static AppointmentsController Create(Result<Guid> bookResult, FakePostkorb postkorb, FakeEmail email)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers();
            var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("name", "Tina Test"),
                new Claim("email", "tina@example.test"),
                new Claim(PostkorbBenachrichtigung.PostkorbHandleClaim, Handle),
            ], "test", "name", null));
            return new AppointmentsController(new FakeAppointments(bookResult), email, postkorb)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user, RequestServices = services.BuildServiceProvider() },
                },
            };
        }

        private static AppointmentCreateRequest Request() => new()
        {
            Service = ServiceType.AllgemeineBeratung,
            Location = LocationType.BuergermtMitte,
            StartUtc = new DateTime(2026, 10, 7, 8, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 10, 7, 8, 15, 0, DateTimeKind.Utc),
        };

        [Fact]
        public async Task Gescheiterte_Buchung_verschickt_keine_Bestaetigung()
        {
            var postkorb = new FakePostkorb();
            var email = new FakeEmail();
            var controller = Create(Result<Guid>.Fail(ErrorCodes.SlotConflict, "Slot belegt"), postkorb, email);

            var result = await controller.Create(Request(), CancellationToken.None);

            Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result.Result).StatusCode);
            Assert.Empty(postkorb.Sent);
            Assert.Equal(0, email.Count);
        }

        [Fact]
        public async Task Erfolgreiche_Buchung_geht_ins_Postfach()
        {
            var postkorb = new FakePostkorb();
            var controller = Create(Result<Guid>.Success(Guid.NewGuid()), postkorb, new FakeEmail());

            var result = await controller.Create(Request(), CancellationToken.None);

            Assert.IsType<CreatedAtActionResult>(result.Result);
            var message = Assert.Single(postkorb.Sent);
            Assert.Equal(Handle, message.PostkorbHandle);
            Assert.Contains("Tina Test", message.Content);
            // 08:00 UTC = 10:00 Uhr deutscher Sommerzeit
            Assert.Contains("07.10.2026, 10:00 Uhr", message.Content);
            Assert.DoesNotContain("\r", message.Content);
            Assert.Equal("zugestellt", controller.Response.Headers[PostkorbBenachrichtigung.StatusHeader].ToString());
        }

        [Fact]
        public async Task Fehlgeschlagene_Zustellung_wird_gemeldet_Buchung_bleibt_erfolgreich()
        {
            var postkorb = new FakePostkorb { Status = PostkorbDeliveryStatus.Failed };
            var controller = Create(Result<Guid>.Success(Guid.NewGuid()), postkorb, new FakeEmail());

            var result = await controller.Create(Request(), CancellationToken.None);

            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal("fehlgeschlagen", controller.Response.Headers[PostkorbBenachrichtigung.StatusHeader].ToString());
        }
    }
}
