using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;

namespace TheWarRoomAPI.Tests
{
    // An empty SQLite database held in memory for one test.
    // Real SQLite is used (not EF's in-memory provider) so that foreign keys, unique indexes and the
    // case-insensitive email column behave exactly as they do when the app runs.
    public sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDatabase()
        {
            // The database lives as long as this connection stays open
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        // A new context each time, so a test reads back what was really saved
        public AppDbContext CreateContext()
        {
            return new AppDbContext(_options);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
