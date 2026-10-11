using BuergerPortal.Api.Postkorb;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Erinnerung vor dem Termin: Nachricht ins eigene Postfach, Benachrichtigung ohne Namen (echter API-Host).</summary>
    public class TerminErinnerungDienstTests
    {
        private sealed class FakePostkorb : IPostkorbService
        {
            public List<PostkorbMessage> Sent { get; } = [];

            public Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default)
            {
                Sent.Add(message);
                return Task.FromResult(PostkorbDeliveryStatus.Delivered);
            }
        }

        [Fact]
        public async Task Faelliger_Termin_wird_einmal_ins_Postfach_erinnert()
        {
            var postkorb = new FakePostkorb();
            using var factory = new ApiTestFactory(services: s =>
            {
                s.RemoveAll<IPostkorbService>();
                s.AddSingleton<IPostkorbService>(postkorb);
            });
            var start = DateTime.UtcNow.AddHours(20);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
                db.Appointments.Add(new Appointment
                {
                    Id = Guid.NewGuid(), Service = ServiceType.AllgemeineBeratung, Location = LocationType.BuergermtNord,
                    StartUtc = start, EndUtc = start.AddMinutes(15), UserId = Guid.NewGuid(), Status = AppointmentStatus.Booked,
                    PostkorbHandle = "511fdf2b-70fc-3dd8-85a7-9ed9a9687910", GebuchtUtc = DateTime.UtcNow.AddDays(-2), RowVersion = [1],
                });
                await db.SaveChangesAsync();
            }
            var dienst = factory.Services.GetRequiredService<TerminErinnerungDienst>();

            Assert.Equal(1, await dienst.RunOnceAsync(CancellationToken.None));
            Assert.Equal(0, await dienst.RunOnceAsync(CancellationToken.None));

            var nachricht = Assert.Single(postkorb.Sent);
            Assert.Equal("511fdf2b-70fc-3dd8-85a7-9ed9a9687910", nachricht.PostkorbHandle);
            Assert.StartsWith("Erinnerung an Ihren Termin:", nachricht.Title);
            Assert.Contains("Bürgeramt Nord", nachricht.Content);
            Assert.DoesNotContain("\r", nachricht.Content);
        }
    }
}
