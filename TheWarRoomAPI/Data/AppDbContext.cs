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

        // Film
        public DbSet<ScoutedTeam> ScoutedTeams { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<Clip> Clips { get; set; }
        public DbSet<PlayTag> PlayTags { get; set; }
        public DbSet<Cutup> Cutups { get; set; }
        public DbSet<CutupClip> CutupClips { get; set; }
        public DbSet<FilmAssignment> FilmAssignments { get; set; }

        // Study time
        public DbSet<ViewingSession> ViewingSessions { get; set; }

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

            // A change request points at two users: who asked for it and who decided on it.
            // One that was sent again after a rejection also points at the request that was rejected.
            modelBuilder.Entity<ApprovalRequest>(entity =>
            {
                entity.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById);
                entity.HasOne(r => r.ReviewedBy).WithMany().HasForeignKey(r => r.ReviewedById);
                entity.HasOne(r => r.ResubmittedFrom).WithMany().HasForeignKey(r => r.ResubmittedFromId);
            });

            // The names of the teams on film are unique without regard to upper/lower case
            modelBuilder.Entity<ScoutedTeam>().Property(t => t.Name).UseCollation("NOCASE");

            // A game points at two teams on film: the home team and the away team
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasOne(g => g.HomeTeam).WithMany().HasForeignKey(g => g.HomeTeamId);
                entity.HasOne(g => g.AwayTeam).WithMany().HasForeignKey(g => g.AwayTeamId);
            });

            // One tag per clip (One-to-One)
            modelBuilder.Entity<PlayTag>(entity =>
            {
                entity.HasOne(t => t.Clip)
                    .WithOne(c => c.Tag)
                    .HasForeignKey<PlayTag>(t => t.ClipId);
            });

            // A film assignment points at two users: the player it is shared with and the coach who shared it
            modelBuilder.Entity<FilmAssignment>(entity =>
            {
                entity.HasOne(a => a.Player).WithMany().HasForeignKey(a => a.PlayerId);
                entity.HasOne(a => a.AssignedBy).WithMany().HasForeignKey(a => a.AssignedById);
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

            // Film: a video's clips, a clip's tag, and a cutup's plays and sharing only exist as part of their parent.
            // (A clip that is used in a cutup still cannot be deleted: the cutup's play points at it without cascade.)
            modelBuilder.Entity<Clip>()
                .HasOne(c => c.Video)
                .WithMany(v => v.Clips)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayTag>()
                .HasOne(t => t.Clip)
                .WithOne(c => c.Tag)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CutupClip>()
                .HasOne(c => c.Cutup)
                .WithMany(c => c.Clips)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FilmAssignment>()
                .HasOne(a => a.Cutup)
                .WithMany(c => c.Assignments)
                .OnDelete(DeleteBehavior.Cascade);

            // When a position group is deleted, film simply stops being shared with it
            modelBuilder.Entity<FilmAssignment>()
                .HasOne(a => a.PositionGroup)
                .WithMany()
                .HasForeignKey(a => a.PositionGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // A player's film time is kept when the cutup that was watched is deleted: the record just stops pointing at it
            modelBuilder.Entity<ViewingSession>()
                .HasOne(v => v.Cutup)
                .WithMany()
                .HasForeignKey(v => v.CutupId)
                .OnDelete(DeleteBehavior.SetNull);

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
