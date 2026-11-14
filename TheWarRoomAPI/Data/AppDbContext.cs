using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Data
{
    public class AppDbContext : DbContext
    {
        //Constructor with options parameter
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Define DbSet for each entity
        public DbSet<Team> Teams { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<LoginSession> LoginSessions { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Every DateTime in the model is stored and read back as UTC
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Email is the login name: unique, and compared without regard to upper/lower case
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Email).UseCollation("NOCASE");
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<LoginSession>(entity =>
            {
                entity.HasIndex(s => s.Token).IsUnique();
            });

            // Deleting a record never silently deletes the records that point to it.
            // A relationship that should cascade is switched on explicitly below this loop.
            foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
