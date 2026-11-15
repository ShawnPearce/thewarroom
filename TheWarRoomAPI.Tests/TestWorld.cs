using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // Sets up teams, coaches and players for a test, and creates the services under test.
    // Accounts are written straight to the database here, which is quicker than going through
    // the services; the tests about creating accounts use the services themselves.
    public sealed class TestWorld : IDisposable
    {
        private int _accountCounter;

        public TestDatabase Database { get; } = new TestDatabase();

        // Wednesday 2 September 2026. The Monday of that week is 31 August.
        public TestClock Clock { get; } = new TestClock(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

        public static readonly DateOnly ThisMonday = new DateOnly(2026, 8, 31);
        public static readonly DateOnly Today = new DateOnly(2026, 9, 2);

        public PasswordHasher<User> PasswordHasher { get; } = new PasswordHasher<User>();

        // Film storage and the video tools are stand-ins, so no test needs a bucket or ffmpeg.
        // The largest upload is set small enough for a test to go over it.
        public FakeFilmStorage Storage { get; } = new FakeFilmStorage();
        public FakeVideoTools Tools { get; } = new FakeVideoTools();
        public FilmOptions Film { get; } = new FilmOptions { MaxUploadBytes = 10_000 };

        // The site's switches. A test changes one to see the rule that depends on it.
        public SiteOptions Site { get; } = new SiteOptions();

        public void Dispose()
        {
            Database.Dispose();
        }

        public AppDbContext NewContext()
        {
            return Database.CreateContext();
        }

        // ---------- Services ----------

        public AuthService Auth(AppDbContext context) => new AuthService(context, PasswordHasher, Clock);
        public UserService Users(AppDbContext context) => new UserService(context, PasswordHasher, Clock);
        public RosterService Roster(AppDbContext context) => new RosterService(context, PasswordHasher, Clock);
        public PositionGroupService Groups(AppDbContext context) => new PositionGroupService(context, Clock);
        public DepthChartService DepthChart(AppDbContext context) => new DepthChartService(context, Clock);
        public ScheduleService Schedule(AppDbContext context) => new ScheduleService(context, Clock);
        public AttendanceService Attendance(AppDbContext context) => new AttendanceService(context, Schedule(context), Clock);
        public TeamService Teams(AppDbContext context) => new TeamService(context, Auth(context), PasswordHasher, Clock, Options.Create(Site));
        public PlaybookService Playbooks(AppDbContext context) => new PlaybookService(context, new PlaybookChangeApplier(context, Clock), Clock);
        public ApprovalService Approvals(AppDbContext context) => new ApprovalService(context, new PlaybookChangeApplier(context, Clock), Clock);
        public ScoutedTeamService ScoutedTeams(AppDbContext context) => new ScoutedTeamService(context);
        public GameService Games(AppDbContext context) => new GameService(context, Clock);
        public ClipService Clips(AppDbContext context) => new ClipService(context, Clock);
        public VideoService Videos(AppDbContext context) => new VideoService(context, Clips(context), Storage, Tools, Options.Create(Film), Clock);
        public CutupService Cutups(AppDbContext context) => new CutupService(context, Storage, Tools, Clock);
        public CutupRenderer Renderer(AppDbContext context) => new CutupRenderer(context, Storage, Tools, Clock, NullLogger<CutupRenderer>.Instance);
        public TendencyService Tendencies(AppDbContext context) => new TendencyService(context);
        public StudyTimeService StudyTime(AppDbContext context) => new StudyTimeService(context, Clock);
        public ActivityService Activity(AppDbContext context) => new ActivityService(context, Clock);

        // ---------- Setting up data ----------

        // A team with the standard groups and positions, and its head coach
        public async Task<CurrentUser> AddTeamAsync(string name = "Test Tigers")
        {
            using var context = NewContext();

            var team = new Team { Name = name, CreatedAt = Clock.GetUtcNow().UtcDateTime };
            var headCoach = NewUser(team, UserRole.HeadCoach, "Head", "Coach");
            headCoach.CanPublishPlaybook = true;

            context.Users.Add(headCoach);
            TeamDefaults.AddTo(team, context, Clock.GetUtcNow().UtcDateTime);
            await context.SaveChangesAsync();

            return await AsAsync(headCoach.Id);
        }

        public async Task<CurrentUser> AddCoachAsync(CurrentUser teamMember, UserRole role, TeamUnit? unit = null, params string[] groupNames)
        {
            using var context = NewContext();

            var coach = NewUser(null, role, "Coach", $"Number{_accountCounter + 1}");
            coach.TeamId = teamMember.TeamId;
            coach.CoordinatorUnit = unit;
            coach.CoachGroupAssignments = new List<CoachGroupAssignment>();
            foreach (var groupName in groupNames)
            {
                coach.CoachGroupAssignments.Add(new CoachGroupAssignment { PositionGroupId = await GroupIdAsync(teamMember, groupName) });
            }

            context.Users.Add(coach);
            await context.SaveChangesAsync();

            return await AsAsync(coach.Id);
        }

        public async Task<CurrentUser> AddPlayerAsync(CurrentUser teamMember, string lastName, params string[] groupNames)
        {
            using var context = NewContext();

            var player = NewUser(null, UserRole.Player, "Player", lastName);
            player.TeamId = teamMember.TeamId;
            player.CanEditDepthChart = false;
            player.CanRecordAttendance = false;
            player.RosterEntry = new RosterEntry { TeamId = teamMember.TeamId, Status = RosterStatus.Active };
            player.PlayerGroupAssignments = new List<PlayerGroupAssignment>();
            foreach (var groupName in groupNames)
            {
                player.PlayerGroupAssignments.Add(new PlayerGroupAssignment { PositionGroupId = await GroupIdAsync(teamMember, groupName) });
            }

            context.Users.Add(player);
            await context.SaveChangesAsync();

            return await AsAsync(player.Id);
        }

        public async Task<int> AddWeekAsync(CurrentUser teamMember, DateOnly monday)
        {
            using var context = NewContext();

            var week = new DepthChartWeek
            {
                TeamId = teamMember.TeamId,
                WeekStartDate = monday,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.DepthChartWeeks.Add(week);
            await context.SaveChangesAsync();

            return week.Id;
        }

        public async Task<int> AddEventAsync(CurrentUser teamMember, DateOnly date, EventType type = EventType.Practice, string title = "Practice")
        {
            using var context = NewContext();

            var scheduledEvent = new ScheduledEvent
            {
                TeamId = teamMember.TeamId,
                EventType = type,
                Title = title,
                EventDate = date,
                StartTime = new TimeOnly(15, 30),
                EndTime = new TimeOnly(17, 30),
                Location = "Practice field",
                CreatedById = teamMember.Id,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.ScheduledEvents.Add(scheduledEvent);
            await context.SaveChangesAsync();

            return scheduledEvent.Id;
        }

        // The head coach's switch for attendance tracking
        public async Task SetAttendanceTrackingAsync(CurrentUser teamMember, bool enabled)
        {
            using var context = NewContext();
            var team = await context.Teams.SingleAsync(t => t.Id == teamMember.TeamId);
            team.AttendanceTrackingEnabled = enabled;
            await context.SaveChangesAsync();
        }

        // ---------- Film ----------

        // What most film tests start from: a game between the team (at home) and one opponent,
        // with a video that is ready and one hour long
        public record TestFilm(int OwnTeamId, int OpponentId, int GameId, int VideoId);

        public const int OneHourMs = 60 * 60 * 1000;

        public async Task<TestFilm> AddFilmAsync(CurrentUser teamMember, string opponent = "Riverside Raiders")
        {
            int ownTeamId = await OwnTeamOnFilmIdAsync(teamMember);
            int opponentId = await AddOpponentAsync(teamMember, opponent);
            int gameId = await AddGameAsync(teamMember, ownTeamId, opponentId);
            int videoId = await AddVideoAsync(teamMember, gameId);

            return new TestFilm(ownTeamId, opponentId, gameId, videoId);
        }

        // The team's own entry among the teams on film (every team is given one)
        public async Task<int> OwnTeamOnFilmIdAsync(CurrentUser teamMember)
        {
            using var context = NewContext();
            return await context.ScoutedTeams
                .Where(t => t.TeamId == teamMember.TeamId && t.IsOwnTeam)
                .Select(t => t.Id)
                .SingleAsync();
        }

        public async Task<int> AddOpponentAsync(CurrentUser teamMember, string name)
        {
            using var context = NewContext();

            var opponent = new ScoutedTeam { TeamId = teamMember.TeamId, Name = name };
            context.ScoutedTeams.Add(opponent);
            await context.SaveChangesAsync();

            return opponent.Id;
        }

        public async Task<int> AddGameAsync(CurrentUser teamMember, int homeTeamId, int awayTeamId, DateOnly? date = null)
        {
            using var context = NewContext();

            var game = new Game
            {
                TeamId = teamMember.TeamId,
                GameDate = date ?? Today.AddDays(-5),
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                CreatedById = teamMember.Id,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.Games.Add(game);
            await context.SaveChangesAsync();

            return game.Id;
        }

        public async Task<int> AddVideoAsync(CurrentUser teamMember, int gameId, VideoStatus status = VideoStatus.Ready, int? durationMs = OneHourMs)
        {
            using var context = NewContext();

            var video = new Video
            {
                TeamId = teamMember.TeamId,
                GameId = gameId,
                Title = "Full game",
                Status = status,
                DurationMs = durationMs,
                UploadedById = teamMember.Id,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.Videos.Add(video);
            await context.SaveChangesAsync();

            return video.Id;
        }

        // A video that went through an upload: it is ready, and its file is in film storage
        public async Task<int> AddVideoWithFileAsync(CurrentUser teamMember, int gameId, string title = "Sideline camera", int durationMs = OneHourMs)
        {
            using var context = NewContext();

            var video = new Video
            {
                TeamId = teamMember.TeamId,
                GameId = gameId,
                Title = title,
                Status = VideoStatus.Ready,
                DurationMs = durationMs,
                StorageKey = $"teams/{teamMember.TeamId}/videos/{Guid.NewGuid():N}.mp4",
                OriginalFileName = "game.mp4",
                ContentType = "video/mp4",
                SizeBytes = 5000,
                UploadedById = teamMember.Id,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            context.Videos.Add(video);
            await context.SaveChangesAsync();

            Storage.BrowserSends(video.StorageKey, 5000);
            return video.Id;
        }

        // A cutup with the given clips in that order, still a draft
        public async Task<int> AddCutupAsync(CurrentUser coach, string name, params int[] clipIds)
        {
            using var context = NewContext();
            var cutups = Cutups(context);

            var cutup = await cutups.CreateAsync(coach, new DTOs.SaveCutupDto { Name = name });
            if (clipIds.Length > 0)
            {
                await cutups.AddClipsAsync(coach, cutup.Id, new DTOs.AddCutupClipsDto { ClipIds = clipIds.ToList() });
            }

            return cutup.Id;
        }

        // A clip written straight to the database. It is tagged when a team on offence is given.
        public async Task<int> AddClipAsync(CurrentUser teamMember, int videoId, int startSeconds, int endSeconds,
            int? offenceTeamId = null, PlayType playType = PlayType.Run, int down = 1, int distance = 10)
        {
            using var context = NewContext();
            var now = Clock.GetUtcNow().UtcDateTime;

            var clip = new Clip
            {
                TeamId = teamMember.TeamId,
                VideoId = videoId,
                StartMs = startSeconds * 1000,
                EndMs = endSeconds * 1000,
                CreatedById = teamMember.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (offenceTeamId != null)
            {
                clip.Tag = new PlayTag
                {
                    TeamId = teamMember.TeamId,
                    PlayType = playType,
                    Down = down,
                    Distance = distance,
                    OffenceTeamId = offenceTeamId.Value
                };
            }

            context.Clips.Add(clip);
            await context.SaveChangesAsync();

            return clip.Id;
        }

        // Several tagged plays of the same kind at once: so many runs and so many passes
        // for one team on one down and distance. They are laid one after another in the video.
        public async Task AddTaggedPlaysAsync(CurrentUser teamMember, int videoId, int offenceTeamId, int down, int distance, int runs, int passes)
        {
            using var context = NewContext();
            var now = Clock.GetUtcNow().UtcDateTime;

            for (int i = 0; i < runs + passes; i++)
            {
                _nextPlaySecond += 8;

                context.Clips.Add(new Clip
                {
                    TeamId = teamMember.TeamId,
                    VideoId = videoId,
                    StartMs = _nextPlaySecond * 1000,
                    EndMs = (_nextPlaySecond + 6) * 1000,
                    CreatedById = teamMember.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Tag = new PlayTag
                    {
                        TeamId = teamMember.TeamId,
                        PlayType = i < runs ? PlayType.Run : PlayType.Pass,
                        Down = down,
                        Distance = distance,
                        OffenceTeamId = offenceTeamId
                    }
                });
            }

            await context.SaveChangesAsync();
        }

        private int _nextPlaySecond;

        // ---------- Study time ----------

        // A sign-in in the past: it started then and its last request came so many minutes later
        public async Task AddLoginSessionAsync(CurrentUser user, DateTime startedAtUtc, int minutes)
        {
            using var context = NewContext();

            context.LoginSessions.Add(new LoginSession
            {
                UserId = user.Id,
                Token = Guid.NewGuid().ToString("N"),
                StartedAt = startedAtUtc,
                LastSeenAt = startedAtUtc.AddMinutes(minutes),
                EndedAt = startedAtUtc.AddMinutes(minutes)
            });
            await context.SaveChangesAsync();
        }

        // Viewing time that was already counted: so many minutes of film or playbook, starting at a moment in the past
        public async Task AddViewingAsync(CurrentUser player, ViewingKind kind, DateTime startedAtUtc, int minutes)
        {
            using var context = NewContext();

            context.ViewingSessions.Add(new ViewingSession
            {
                TeamId = player.TeamId,
                UserId = player.Id,
                Kind = kind,
                PlaybookId = kind == ViewingKind.Playbook
                    ? await context.Playbooks.Where(p => p.TeamId == player.TeamId).Select(p => (int?)p.Id).FirstAsync()
                    : null,
                PageKey = Guid.NewGuid().ToString("N"),
                StartedAt = startedAtUtc,
                LastCreditAt = startedAtUtc.AddMinutes(minutes),
                ActiveMs = minutes * 60 * 1000
            });
            await context.SaveChangesAsync();
        }

        // ---------- Looking things up ----------

        // The user as the API would see them on their next request (role, permissions and groups re-read)
        public async Task<CurrentUser> AsAsync(int userId)
        {
            using var context = NewContext();
            var currentUser = await Auth(context).ResolveCurrentUserAsync(userId, null);
            return currentUser ?? throw new InvalidOperationException($"User {userId} could not be resolved.");
        }

        public async Task<int> GroupIdAsync(CurrentUser teamMember, string groupName)
        {
            using var context = NewContext();
            return await context.PositionGroups
                .Where(g => g.TeamId == teamMember.TeamId && g.Name == groupName)
                .Select(g => g.Id)
                .SingleAsync();
        }

        public async Task<int> PositionIdAsync(CurrentUser teamMember, string code)
        {
            using var context = NewContext();
            return await context.Positions
                .Where(p => p.TeamId == teamMember.TeamId && p.Code == code)
                .Select(p => p.Id)
                .SingleAsync();
        }

        public async Task<int> PlaybookIdAsync(CurrentUser teamMember, TeamUnit unit)
        {
            using var context = NewContext();
            return await context.Playbooks
                .Where(p => p.TeamId == teamMember.TeamId && p.Unit == unit)
                .Select(p => p.Id)
                .SingleAsync();
        }

        public async Task<int> FormationIdAsync(CurrentUser teamMember, string name)
        {
            using var context = NewContext();
            return await context.Formations
                .Where(f => f.TeamId == teamMember.TeamId && f.Name == name)
                .Select(f => f.Id)
                .SingleAsync();
        }

        // The diagram of one of the team's formations, with a route added to the named players.
        // This is what the play designer would send for a simple play.
        public async Task<string> DiagramAsync(CurrentUser teamMember, string formationName, params string[] labelsWithARoute)
        {
            using var context = NewContext();
            string json = await context.Formations
                .Where(f => f.TeamId == teamMember.TeamId && f.Name == formationName)
                .Select(f => f.DiagramJson)
                .SingleAsync();

            var diagram = DiagramValidator.Parse(json);
            foreach (var label in labelsWithARoute)
            {
                diagram.Players.First(p => p.Label == label).Paths.Add(new DiagramPath
                {
                    Kind = "route",
                    Pts = new List<double[]> { new[] { 0.0, 5.0 } }
                });
            }

            return DiagramValidator.Serialize(diagram);
        }

        private User NewUser(Team? team, UserRole role, string firstName, string lastName)
        {
            _accountCounter++;

            return new User
            {
                Team = team,
                FirstName = firstName,
                LastName = lastName,
                Email = $"user{_accountCounter}@example.com",
                PasswordHash = "not-a-real-hash",
                Role = role,
                CanEditDepthChart = true,
                CanRecordAttendance = true,
                IsActive = true,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
        }
    }
}
