using System.Data;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Appointments.Enums;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuergerPortal.Infrastructure.Repositories
{
    /// <summary>
    /// Sperre beim Buchen über SQL Server: eine Transaktion auf dem DbContext und je Person und Standort eine
    /// Anwendungssperre (<c>sp_getapplock</c>, exklusiv, an die Transaktion gebunden). Prüfung und Speichern laufen in
    /// derselben Transaktion; Commit oder Rollback geben die Sperren frei. Wirkt auch, wenn mehrere API-Instanzen
    /// dieselbe Datenbank nutzen. Mit dem InMemory-Anbieter (Tests) gibt es keine Transaktionen – dann ohne Sperre.
    /// </summary>
    public sealed class SqlServerAppointmentBookingLock(PortalDbContext db) : IAppointmentBookingLock
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public async Task<IAppointmentBookingLease> AcquireAsync(Guid userId, LocationType location, CancellationToken ct)
        {
            if (!db.Database.IsSqlServer())
            {
                return NoLease.Instance;
            }

            var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                // Feste Reihenfolge Person → Standort: Zwei Buchungen warten nie wechselseitig aufeinander.
                await GetAppLockAsync($"bpsim:termin:person:{userId:N}", ct);
                await GetAppLockAsync($"bpsim:termin:standort:{(int)location}", ct);
                return new Lease(transaction);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        private async Task GetAppLockAsync(string resource, CancellationToken ct)
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await db.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                [result, new SqlParameter("@resource", resource), new SqlParameter("@timeout", (int)Timeout.TotalMilliseconds)],
                ct);
            // 0/1 = erhalten, < 0 = Zeitüberschreitung, Abbruch oder Deadlock-Opfer
            if (result.Value is not int code || code < 0)
            {
                throw new TimeoutException($"Sperre {resource} nicht erhalten (sp_getapplock: {result.Value}).");
            }
        }

        private sealed class Lease(IDbContextTransaction transaction) : IAppointmentBookingLease
        {
            public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

            // ohne Commit: Rollback – die Sperren werden in beiden Fällen frei
            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }

        private sealed class NoLease : IAppointmentBookingLease
        {
            public static readonly NoLease Instance = new();
            public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
