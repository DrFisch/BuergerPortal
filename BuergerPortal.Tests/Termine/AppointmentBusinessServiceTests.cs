using BuergerPortal.Application.Appointments.BusinessServices;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Appointments.Validation;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Entity;
using BuergerPortal.Domain.Appointments.Enums;

namespace BuergerPortal.Tests.Termine
{
    /// <summary>Belegung je Standort, eigene Überschneidungen, Storno und Standortwechsel.</summary>
    public class AppointmentBusinessServiceTests
    {
        private static readonly AppointmentValidatorTests.FixedTime Now =
            new(new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero)); // Mi 07.10.2026, 10:00 Ortszeit

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bernd = Guid.NewGuid();

        /// <summary>Repository im Speicher mit derselben Überschneidungslogik wie die EF-Fassung.</summary>
        private sealed class MemoryRepository : IAppointmentRepository
        {
            public List<Appointment> Items { get; } = [];

            private static bool Overlaps(Appointment a, DateTime start, DateTime end) =>
                a.Status == AppointmentStatus.Booked && a.StartUtc < end && start < a.EndUtc;

            public Task<bool> ExistsOverlapAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
                Task.FromResult(Items.Any(a => a.UserId == userId && Overlaps(a, startUtc, endUtc)));

            public Task<bool> ExistsLocationOverlapAsync(LocationType location, DateTime startUtc, DateTime endUtc,
                Guid? excludeId, CancellationToken ct) =>
                Task.FromResult(Items.Any(a => a.Location == location && a.Id != excludeId && Overlaps(a, startUtc, endUtc)));

            public Task CreateAsync(Appointment entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }
            public Task<List<Appointment>> GetAllForUserAsync(Guid userId, CancellationToken ct) =>
                Task.FromResult(Items.Where(a => a.UserId == userId).ToList());
            public Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, LocationType? location,
                CancellationToken ct) =>
                Task.FromResult(Items.Where(a => (location == null || a.Location == location) && Overlaps(a, fromUtc, toUtc)).ToList());
            public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
                Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
            public Task UpdateAsync(Appointment entity, CancellationToken ct) => Task.CompletedTask;
            public Task DeleteAsync(Appointment entity, CancellationToken ct) { Items.Remove(entity); return Task.CompletedTask; }
        }

        private readonly MemoryRepository _repo = new();
        private AppointmentBusinessService Service => new(_repo, new AppointmentCreateDtoValidator(Now), Now);

        // Do 08.10.2026, Ortszeit UTC+2
        private static AppointmentCreateDto Dto(LocationType ort, int stunde, int minute, int dauer = 15)
        {
            var start = new DateTime(2026, 10, 8, stunde - 2, minute, 0, DateTimeKind.Utc);
            return new() { Service = ServiceType.AllgemeineBeratung, Location = ort, StartUtc = start, EndUtc = start.AddMinutes(dauer) };
        }

        [Fact]
        public async Task Zweite_Person_kann_denselben_Termin_am_selben_Standort_nicht_buchen()
        {
            Assert.True((await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0, 30), Anna, default)).IsSuccess);

            var result = await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 15), Bernd, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.SlotConflict, result.ErrorCode);
            Assert.Equal(AppointmentBusinessService.LocationTakenMessage, result.ErrorMessage);
        }

        [Fact]
        public async Task Gleiche_Zeit_an_anderem_Standort_ist_fuer_andere_Person_frei()
        {
            await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0), Anna, default);
            Assert.True((await Service.BookAsync(Dto(LocationType.BuergermtNord, 9, 0), Bernd, default)).IsSuccess);
        }

        [Fact]
        public async Task Eigene_Ueberschneidung_an_anderem_Standort_wird_verstaendlich_gemeldet()
        {
            await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0, 45), Anna, default);

            var result = await Service.BookAsync(Dto(LocationType.BuergermtSued, 9, 30), Anna, default);

            Assert.Equal(AppointmentBusinessService.OwnOverlapMessage, result.ErrorMessage);
        }

        [Fact]
        public async Task Anschlusstermin_direkt_danach_ist_erlaubt()
        {
            await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0, 30), Anna, default);
            Assert.True((await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 30), Bernd, default)).IsSuccess);
        }

        [Fact]
        public async Task Belegte_Zeiten_nur_fuer_den_gewaehlten_Standort()
        {
            await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0), Anna, default);
            await Service.BookAsync(Dto(LocationType.BuergermtNord, 10, 0), Bernd, default);
            var tag = new DateTime(2026, 10, 7, 22, 0, 0, DateTimeKind.Utc);

            var mitte = await Service.GetBusyAsync(tag, tag.AddDays(1), LocationType.BuergermtMitte, default);
            var alle = await Service.GetBusyAsync(tag, tag.AddDays(1), null, default);

            Assert.Single(mitte);
            Assert.Equal(2, alle.Count);
        }

        [Fact]
        public async Task Vergangener_Termin_kann_nicht_storniert_werden()
        {
            var vergangen = new Appointment
            {
                Id = Guid.NewGuid(), UserId = Anna, Location = LocationType.BuergermtMitte, Status = AppointmentStatus.Booked,
                StartUtc = new DateTime(2026, 10, 6, 7, 0, 0, DateTimeKind.Utc), EndUtc = new DateTime(2026, 10, 6, 7, 15, 0, DateTimeKind.Utc)
            };
            _repo.Items.Add(vergangen);

            var result = await Service.CancelAsync(vergangen.Id, Anna, default);

            Assert.Equal("Vergangene Termine können nicht storniert werden.", result.ErrorMessage);
            Assert.Equal(AppointmentStatus.Booked, vergangen.Status);
        }

        [Fact]
        public async Task Standortwechsel_nur_wenn_die_Zeit_dort_frei_ist()
        {
            var anna = await Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0), Anna, default);
            await Service.BookAsync(Dto(LocationType.BuergermtNord, 9, 0), Bernd, default);

            var belegt = await Service.UpdateLocationAsync(anna.Value, Anna, LocationType.BuergermtNord, default);
            var gleich = await Service.UpdateLocationAsync(anna.Value, Anna, LocationType.BuergermtMitte, default);
            var frei = await Service.UpdateLocationAsync(anna.Value, Anna, LocationType.BuergermtSued, default);

            Assert.Equal(ErrorCodes.SlotConflict, belegt.ErrorCode);
            Assert.Equal(ErrorCodes.Validation, gleich.ErrorCode);
            Assert.True(frei.IsSuccess);
        }
    }
}
