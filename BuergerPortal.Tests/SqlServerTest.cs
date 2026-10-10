using BuergerPortal.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BuergerPortal.Tests
{
    /// <summary>
    /// Tests gegen einen echten SQL Server. Verbindung aus BPSIM_TEST_SQL, sonst die eigene LocalDB-Test-DB
    /// BuergerPortalDB_bpsim. Ohne erreichbaren SQL Server (z. B. in der CI unter Linux) werden die Tests übersprungen.
    /// </summary>
    public static class SqlServerTest
    {
        public static readonly string ConnectionString = Environment.GetEnvironmentVariable("BPSIM_TEST_SQL")
            ?? @"Server=(localdb)\mssqllocaldb;Database=BuergerPortalDB_bpsim;Trusted_Connection=True;Connect Timeout=5";

        public static PortalDbContext NewContext() =>
            new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlServer(ConnectionString).Options);
    }

    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            try
            {
                using var connection = new SqlConnection(SqlServerTest.ConnectionString);
                connection.Open();
            }
            catch (Exception ex)
            {
                Skip = $"kein SQL Server erreichbar ({ex.GetType().Name})";
            }
        }
    }
}
