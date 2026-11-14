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

        // Schedule and attendance
        public DbSet<ScheduledEvent> ScheduledEvents { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

        // Playbook
        public DbSet<Playbook> Playbooks { get; set; }
        public DbSet<Formation> Formations { get; set; }
        public DbSet<Play> Plays { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }

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

            // An attendance record points at two users: the player it is about and the coach who recorded it
            modelBuilder.Entity<AttendanceRecord>(entity =>
            {
                entity.HasOne(a => a.Player).WithMany().HasForeignKey(a => a.PlayerId);
                entity.HasOne(a => a.RecordedBy).WithMany().HasForeignKey(a => a.RecordedById);
            });

            // Formation and play names are unique without regard to upper/lower case
            modelBuilder.Entity<Formation>().Property(f => f.Name).UseCollation("NOCASE");
            modelBuilder.Entity<Play>().Property(p => p.Name).UseCollation("NOCASE");

            // A change request points at two users: who asked for it and who decided on it
            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById);
                entity.HasOne(r => r.ReviewedBy).WithMany().HasForeignKey(r => r.ReviewedById);
            });

            // Deleting a record never silently deletes the records that point to it.
            // A relationship that should cascade is switched on explicitly below this loop.
            foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // Records that only exist as part of their parent are deleted with it:
            // a week's placements, a group's coach and player assignments, and an event's attendance.
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

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(a => a.ScheduledEvent)
                .WithMany(e => e.AttendanceRecords)
                .OnDelete(DeleteBehavior.Cascade);

            // The history of playbook changes is kept when the formation or play it was about is deleted:
            // the request just stops pointing at it.
            modelBuilder.Entity<ApprovalRequest>()
                .HasOne(r => r.Formation)
                .WithMany()
                .HasForeignKey(r => r.FormationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ApprovalRequest>()
                .HasOne(r => r.Play)
                .WithMany()
                .HasForeignKey(r => r.PlayId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
