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

        // Team and people
        public DbSet<Team> Teams { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<LoginSession> LoginSessions { get; set; }
        public DbSet<RosterEntry> RosterEntries { get; set; }

        // Groups and positions
        public DbSet<PositionGroup> PositionGroups { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<CoachGroupAssignment> CoachGroupAssignments { get; set; }
        public DbSet<PlayerGroupAssignment> PlayerGroupAssignments { get; set; }

        // Depth chart
        public DbSet<DepthChartWeek> DepthChartWeeks { get; set; }
        public DbSet<DepthChartPlacement> DepthChartPlacements { get; set; }

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

            // One roster entry per player (One-to-One)
            modelBuilder.Entity<RosterEntry>(entity =>
            {
                entity.HasOne(r => r.User)
                    .WithOne(u => u.RosterEntry)
                    .HasForeignKey<RosterEntry>(r => r.UserId);
            });

            // Group names and position codes are unique without regard to upper/lower case
            modelBuilder.Entity<PositionGroup>().Property(g => g.Name).UseCollation("NOCASE");
            modelBuilder.Entity<Position>().Property(p => p.Code).UseCollation("NOCASE");

            // Deleting a record never silently deletes the records that point to it.
            // A relationship that should cascade is switched on explicitly below this loop.
            foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // Records that only exist as part of their parent are deleted with it:
            // a week's placements, and a group's coach and player assignments.
            modelBuilder.Entity<DepthChartPlacement>()
                .HasOne(p => p.DepthChartWeek)
                .WithMany(w => w.Placements)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CoachGroupAssignment>()
                .HasOne(a => a.PositionGroup)
                .WithMany(g => g.CoachAssignments)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerGroupAssignment>()
                .HasOne(a => a.PositionGroup)
                .WithMany(g => g.PlayerAssignments)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
