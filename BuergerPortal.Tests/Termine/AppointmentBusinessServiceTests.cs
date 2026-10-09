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

            /// <summary>Verzögerung zwischen Prüfen und Speichern – macht gleichzeitige Anfragen im Test sichtbar.</summary>
            public TimeSpan CheckDelay { get; set; } = TimeSpan.Zero;

            private static bool Overlaps(Appointment a, DateTime start, DateTime end) =>
                a.Status == AppointmentStatus.Booked && a.StartUtc < end && start < a.EndUtc;

            public Task<bool> ExistsOverlapAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
                Task.FromResult(Items.Any(a => a.UserId == userId && Overlaps(a, startUtc, endUtc)));

            public async Task<bool> ExistsLocationOverlapAsync(LocationType location, DateTime startUtc, DateTime endUtc,
                Guid? excludeId, CancellationToken ct)
            {
                bool taken;
                lock (Items) taken = Items.Any(a => a.Location == location && a.Id != excludeId && Overlaps(a, startUtc, endUtc));
                if (CheckDelay > TimeSpan.Zero) await Task.Delay(CheckDelay, ct);
                return taken;
            }

            public Task CreateAsync(Appointment entity, CancellationToken ct) { lock (Items) Items.Add(entity); return Task.CompletedTask; }
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

        /// <summary>Sperre im Speicher (wie sp_getapplock: erst Person, dann Standort, exklusiv).</summary>
        private sealed class MemoryLock : IAppointmentBookingLock
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

            public async Task<IAppointmentBookingLease> AcquireAsync(Guid userId, LocationType location, CancellationToken ct)
            {
                var person = _locks.GetOrAdd($"person:{userId}", _ => new SemaphoreSlim(1, 1));
                var standort = _locks.GetOrAdd($"standort:{location}", _ => new SemaphoreSlim(1, 1));
                await person.WaitAsync(ct);
                await standort.WaitAsync(ct);
                return new Lease(person, standort);
            }

            private sealed class Lease(SemaphoreSlim person, SemaphoreSlim standort) : IAppointmentBookingLease
            {
                public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
                public ValueTask DisposeAsync() { standort.Release(); person.Release(); return ValueTask.CompletedTask; }
            }
        }

        /// <summary>Keine Sperre – so verhielt sich das Buchen vor 6.48.</summary>
        private sealed class NoLock : IAppointmentBookingLock
        {
            public Task<IAppointmentBookingLease> AcquireAsync(Guid userId, LocationType location, CancellationToken ct) =>
                Task.FromResult<IAppointmentBookingLease>(new Lease());

            private sealed class Lease : IAppointmentBookingLease
            {
                public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
        }

        private readonly MemoryRepository _repo = new();
        private readonly MemoryLock _lock = new();
        private AppointmentBusinessService Service => new(_repo, new AppointmentCreateDtoValidator(Now), Now, _lock);

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
        public async Task Gleichzeitige_Buchungen_desselben_Zeitraums_nur_eine_gelingt()
        {
            _repo.CheckDelay = TimeSpan.FromMilliseconds(100);   // beide Anfragen prüfen „gleichzeitig“

            var results = await Task.WhenAll(
                Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0, 30), Anna, default),
                Service.BookAsync(Dto(LocationType.BuergermtMitte, 9, 15), Bernd, default));

            Assert.Single(results, r => r.IsSuccess);
            Assert.Single(_repo.Items);
            Assert.Equal(AppointmentBusinessService.LocationTakenMessage, results.Single(r => !r.IsSuccess).ErrorMessage);
        }

        [Fact]
        public async Task Ohne_Sperre_gingen_beide_gleichzeitigen_Buchungen_durch()
        {
            // Nachweis des Fehlers vor 6.48: Prüfen und Speichern ohne Sperre
            _repo.CheckDelay = TimeSpan.FromMilliseconds(100);
            var ungeschuetzt = new AppointmentBusinessService(_repo, new AppointmentCreateDtoValidator(Now), Now, new NoLock());

            var results = await Task.WhenAll(
                ungeschuetzt.BookAsync(Dto(LocationType.BuergermtMitte, 9, 0, 30), Anna, default),
                ungeschuetzt.BookAsync(Dto(LocationType.BuergermtMitte, 9, 15), Bernd, default));

            Assert.All(results, r => Assert.True(r.IsSuccess));
            Assert.Equal(2, _repo.Items.Count);   // Doppelbuchung am selben Schalter
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
