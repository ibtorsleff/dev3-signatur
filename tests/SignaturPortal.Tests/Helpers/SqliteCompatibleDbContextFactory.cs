using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SignaturPortal.Infrastructure.Data;

namespace SignaturPortal.Tests.Helpers;

/// <summary>
/// Helpers for creating SQLite in-memory databases for integration tests against SignaturDbContext.
///
/// Two problems are solved:
/// 1. SQLite cannot parse "nvarchar(max)" / "varbinary(max)" in DDL (EnsureCreated fails).
///    Fix: strip "(max)" column type hints so EF Core uses SQLite type affinity instead.
///
/// 2. SQLite enforces FK constraints during DML, but test seed data intentionally omits
///    referenced rows (e.g. User records for CreatedBy, template lookup tables).
///    FK enforcement is a SQL Server responsibility; disabling it for test connections is safe.
///    Fix: run "PRAGMA foreign_keys = OFF" on the connection after opening it.
///
/// Usage:
///   var conn = SqliteCompatibleDbContextFactory.OpenConnection();
///   var options = new DbContextOptionsBuilder&lt;SignaturDbContext&gt;().UseSqlite(conn).Options;
///   SqliteCompatibleDbContextFactory.EnsureSchema(options);
///   // ... seed data, run tests
///   conn.Dispose();
/// </summary>
public static class SqliteCompatibleDbContextFactory
{
    /// <summary>
    /// Opens a new in-memory SQLite connection with FK enforcement disabled.
    /// The caller is responsible for disposing the connection after the test.
    /// </summary>
    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF";
        cmd.ExecuteNonQuery();
        return conn;
    }

    /// <summary>
    /// Runs Database.EnsureCreated() against the supplied options using a SQLite-patched context,
    /// then creates any additional tables that are referenced by raw SQL in services but are not
    /// part of the EF model (so EnsureCreated skips them). These tables must exist for raw SQL
    /// queries to parse successfully even when they return no rows.
    /// </summary>
    public static void EnsureSchema(DbContextOptions<SignaturDbContext> options)
    {
        using var db = new PatchedDbContext(options);
        db.Database.EnsureCreated();

        // Create tables referenced by ErActivityService raw SQL but absent from the EF schema.
        // Minimal column sets sufficient for the SQL to parse and return empty results.
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS ErCandidateUser (
                CandidateId INTEGER NOT NULL,
                UserId TEXT NOT NULL
            )
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS ERCandidateEvaluation (
                ERCandidateEvaluationId INTEGER PRIMARY KEY,
                ERCandidateId INTEGER NOT NULL,
                ERActivityMemberId INTEGER NOT NULL
            )
            """);
    }

    /// <summary>
    /// Strips column types containing "(max)" from the EF model so that SQLite can parse the
    /// generated CREATE TABLE statements. Only used for schema creation in tests.
    /// </summary>
    private sealed class PatchedDbContext : SignaturDbContext
    {
        public PatchedDbContext(DbContextOptions<SignaturDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    var columnType = property.GetColumnType();
                    if (columnType != null &&
                        columnType.Contains("(max)", StringComparison.OrdinalIgnoreCase))
                    {
                        property.SetColumnType(null);
                    }
                }
            }
        }
    }
}
