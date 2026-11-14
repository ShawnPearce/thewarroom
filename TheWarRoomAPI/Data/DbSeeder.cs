using Microsoft.AspNetCore.Identity;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Data
{
    // Adds demo teams to an empty database so the app has something to show.
    // It runs at startup when "Seed:DemoData" is true (see appsettings.Development.json).
    // Every demo account uses the password in "Seed:DemoPassword".
    //
    // After changing this file, delete warroom.db and start the API again to get the new demo data.
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context, string demoPassword, TimeProvider clock)
        {
            // The demo data is added only once
            if (context.Teams.Any())
            {
                return;
            }

            var now = clock.GetUtcNow().UtcDateTime;
            var today = WeekDates.Today(clock);
            var thisMonday = WeekDates.MondayOf(today);

            // All demo accounts have the same password, so it is hashed once and the hash is shared
            var passwordHash = new PasswordHasher<User>().HashPassword(new User(), demoPassword);

            SeedLakeshoreLions(context, passwordHash, now, today, thisMonday);
            SeedNorthgateKnights(context, passwordHash, now, thisMonday);
        }

        // The main demo team: a full staff, a roster, two weeks of depth charts, and a schedule with attendance
        private static void SeedLakeshoreLions(AppDbContext context, string passwordHash, DateTime now, DateOnly today, DateOnly thisMonday)
        {
            var lions = new DemoTeam(context, "Lakeshore Lions", "lakeshorelions.example", passwordHash, now);
            lions.Team.AttendanceTrackingEnabled = true;

            // ----- Coaching staff -----
            lions.AddHeadCoach("Marcus", "Bennett", "coach.bennett");

            var offenceCoordinator = lions.AddCoach("Elena", "Ruiz", UserRole.Coordinator, TeamUnit.Offence,
                "Quarterbacks", "Running Backs");
            offenceCoordinator.CanPublishPlaybook = true;   // the head coach has given her the publish right

            lions.AddCoach("Darnell", "Brooks", UserRole.Coordinator, TeamUnit.Defence);
            lions.AddCoach("Priya", "Nair", UserRole.Coordinator, TeamUnit.SpecialTeams, "Specialists");

            lions.AddCoach("Tom", "Gallagher", UserRole.PositionCoach, null, "Receivers", "Tight Ends");
            lions.AddCoach("Sam", "Whitaker", UserRole.PositionCoach, null, "Offensive Line");
            lions.AddCoach("Andre", "Mitchell", UserRole.PositionCoach, null, "Defensive Line", "Linebackers");

            var defensiveBacksCoach = lions.AddCoach("Kenji", "Sato", UserRole.PositionCoach, null, "Defensive Backs");
            defensiveBacksCoach.CanEditDepthChart = false;   // switched off by the head coach, to show that the switch works

            // ----- Players (jersey, name, grade, groups) -----
            // Several players are in more than one group, some on both offence and defence
            lions.AddPlayer(7, "Jalen", "Carter", 12, "Quarterbacks");
            lions.AddPlayer(12, "Owen", "Fischer", 10, "Quarterbacks", "Specialists");

            lions.AddPlayer(22, "Malik", "Thompson", 11, "Running Backs", "Specialists");
            var injuredBack = lions.AddPlayer(28, "Ryan", "O'Connor", 10, "Running Backs");
            injuredBack.RosterEntry!.Status = RosterStatus.Injured;
            lions.AddPlayer(44, "Diego", "Ramirez", 12, "Running Backs", "Linebackers");

            lions.AddPlayer(1, "Tyrese", "Walker", 12, "Receivers", "Specialists");
            lions.AddPlayer(11, "Noah", "Kim", 11, "Receivers", "Defensive Backs");
            lions.AddPlayer(81, "Caleb", "Hughes", 11, "Receivers");
            lions.AddPlayer(84, "Liam", "Novak", 10, "Receivers", "Defensive Backs");

            lions.AddPlayer(88, "Ethan", "Brooks", 12, "Tight Ends");
            lions.AddPlayer(85, "Hassan", "Ali", 11, "Tight Ends");

            lions.AddPlayer(70, "Ben", "Kowalski", 12, "Offensive Line");
            lions.AddPlayer(64, "Aiden", "Murphy", 11, "Offensive Line");
            lions.AddPlayer(55, "Lucas", "Petrov", 12, "Offensive Line", "Specialists");
            lions.AddPlayer(66, "Mateo", "Garcia", 11, "Offensive Line");
            lions.AddPlayer(77, "Connor", "Walsh", 12, "Offensive Line");
            var newPlayer = lions.AddPlayer(62, "Jordan", "Lee", 10, "Offensive Line");
            newPlayer.MustChangePassword = true;   // still has a temporary password, to show the first sign-in

            lions.AddPlayer(90, "Andre", "Jackson", 12, "Defensive Line");
            lions.AddPlayer(95, "Samir", "Patel", 11, "Defensive Line");
            lions.AddPlayer(99, "Tyler", "Grant", 12, "Defensive Line");
            lions.AddPlayer(91, "Chris", "Bell", 11, "Defensive Line");

            lions.AddPlayer(52, "Dominic", "Ford", 12, "Linebackers");
            lions.AddPlayer(50, "Isaiah", "Cole", 11, "Linebackers");

            lions.AddPlayer(21, "Xavier", "Moore", 12, "Defensive Backs");
            lions.AddPlayer(24, "Devon", "Price", 11, "Defensive Backs");
            lions.AddPlayer(3, "Elijah", "Stone", 12, "Defensive Backs");
            lions.AddPlayer(9, "Kai", "Tanaka", 11, "Defensive Backs");

            lions.AddPlayer(5, "Finn", "Larsen", 11, "Specialists");

            context.SaveChanges();

            // ----- Depth charts: last week and this week -----
            // Each line is a position followed by its players' jersey numbers, starter first.
            // #1 Tyrese Walker starts at X and is the backup at Y.
            var chart = new Dictionary<string, int[]>
            {
                ["QB"] = new[] { 7, 12 },
                ["RB"] = new[] { 22, 28 },
                ["FB"] = new[] { 44 },
                ["X"] = new[] { 1, 84 },
                ["Y"] = new[] { 81, 1 },
                ["Z"] = new[] { 11, 84 },
                ["TE"] = new[] { 88, 85 },
                ["LT"] = new[] { 70, 62 },
                ["LG"] = new[] { 64, 62 },
                ["C"] = new[] { 55 },
                ["RG"] = new[] { 66 },
                ["RT"] = new[] { 77 },
                ["LE"] = new[] { 90 },
                ["DT"] = new[] { 95 },
                ["NT"] = new[] { 99 },
                ["RE"] = new[] { 91 },
                ["SLB"] = new[] { 44 },
                ["MLB"] = new[] { 52 },
                ["WLB"] = new[] { 50 },
                ["LCB"] = new[] { 21, 11 },
                ["RCB"] = new[] { 24, 84 },
                ["FS"] = new[] { 3 },
                ["SS"] = new[] { 9 },
                ["K"] = new[] { 5 },
                ["P"] = new[] { 5 },
                ["LS"] = new[] { 55 },
                ["H"] = new[] { 12 },
                ["KR"] = new[] { 22, 1 },
                ["PR"] = new[] { 1, 22 }
            };
            lions.AddDepthChartWeek(thisMonday.AddDays(-7), "vs Riverside", chart);

            // This week #28 is injured, so #44 moves ahead of him at running back
            chart["RB"] = new[] { 22, 44, 28 };
            lions.AddDepthChartWeek(thisMonday, "vs Northgate", chart);

            // ----- Schedule: last week, this week and the start of next week -----
            var practiceStart = new TimeOnly(15, 30);
            var practiceEnd = new TimeOnly(17, 30);
            const string practiceField = "Lakeshore practice field";

            var lastTuesday = lions.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(-6), practiceStart, practiceEnd, practiceField);
            var lastThursday = lions.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(-4), practiceStart, practiceEnd, practiceField);
            var lastGame = lions.AddEvent(EventType.Game, "at Riverside Raiders", thisMonday.AddDays(-3), new TimeOnly(19, 0), null, "Riverside Stadium");
            var filmReview = lions.AddEvent(EventType.Meeting, "Film review: Riverside", thisMonday, new TimeOnly(15, 15), new TimeOnly(16, 0), "Room 114");
            var tuesday = lions.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(1), practiceStart, practiceEnd, practiceField);
            var thursday = lions.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(3), practiceStart, practiceEnd, practiceField);
            var homeGame = lions.AddEvent(EventType.Game, "vs Northgate Knights", thisMonday.AddDays(4), new TimeOnly(19, 0), null, "Lakeshore Field");
            lions.AddEvent(EventType.Meeting, "Film review: Northgate", thisMonday.AddDays(7), new TimeOnly(15, 15), new TimeOnly(16, 0), "Room 114");
            lions.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(8), practiceStart, practiceEnd, practiceField);

            // ----- Attendance for the events that have already happened -----
            // Everyone is present unless listed here by jersey number. #28 is injured, so he is excused throughout.
            var exceptions = new Dictionary<ScheduledEvent, Dictionary<int, AttendanceStatus>>
            {
                [lastTuesday] = new() { [28] = AttendanceStatus.Excused, [84] = AttendanceStatus.Late, [62] = AttendanceStatus.Absent },
                [lastThursday] = new() { [28] = AttendanceStatus.Excused, [9] = AttendanceStatus.Late },
                [lastGame] = new() { [28] = AttendanceStatus.Excused },
                [filmReview] = new() { [28] = AttendanceStatus.Excused, [50] = AttendanceStatus.Absent },
                [tuesday] = new() { [28] = AttendanceStatus.Excused, [84] = AttendanceStatus.Late },
                [thursday] = new() { [28] = AttendanceStatus.Excused },
                [homeGame] = new() { [28] = AttendanceStatus.Excused }
            };
            foreach (var entry in exceptions)
            {
                if (entry.Key.EventDate < today)
                {
                    lions.AddAttendance(entry.Key, entry.Value);
                }
            }

            context.SaveChanges();
        }

        // A second, small team. It exists to show that one team can never see another team's data.
        private static void SeedNorthgateKnights(AppDbContext context, string passwordHash, DateTime now, DateOnly thisMonday)
        {
            var knights = new DemoTeam(context, "Northgate Knights", "northgateknights.example", passwordHash, now);

            knights.AddHeadCoach("Dana", "Whitfield", "coach.whitfield");
            knights.AddCoach("Luis", "Ortega", UserRole.Coordinator, TeamUnit.Offence, "Quarterbacks");

            knights.AddPlayer(10, "Sam", "Reed", 12, "Quarterbacks");
            knights.AddPlayer(20, "Marco", "Diaz", 11, "Running Backs");
            knights.AddPlayer(80, "Jay", "Park", 11, "Receivers");

            context.SaveChanges();

            knights.AddDepthChartWeek(thisMonday, "vs Lakeshore", new Dictionary<string, int[]>
            {
                ["QB"] = new[] { 10 },
                ["RB"] = new[] { 20 },
                ["X"] = new[] { 80 }
            });

            // This team has attendance tracking switched off, so its event has no attendance
            knights.AddEvent(EventType.Practice, "Practice", thisMonday.AddDays(8), new TimeOnly(16, 0), new TimeOnly(18, 0), "Northgate field");

            context.SaveChanges();
        }

        // Builds one demo team step by step
        private class DemoTeam
        {
            private readonly AppDbContext _context;
            private readonly string _emailDomain;
            private readonly string _passwordHash;
            private readonly DateTime _now;
            private readonly Dictionary<int, User> _playersByJersey = new Dictionary<int, User>();

            public Team Team { get; }
            public User? HeadCoach { get; private set; }

            public DemoTeam(AppDbContext context, string name, string emailDomain, string passwordHash, DateTime now)
            {
                _context = context;
                _emailDomain = emailDomain;
                _passwordHash = passwordHash;
                _now = now;

                Team = new Team { Name = name, CreatedAt = now };
                context.Teams.Add(Team);

                // The same groups and positions a newly registered team gets
                TeamDefaults.AddTo(Team, context);
                context.SaveChanges();
            }

            public User AddHeadCoach(string firstName, string lastName, string emailName)
            {
                var headCoach = NewUser(firstName, lastName, UserRole.HeadCoach, null);
                headCoach.Email = $"{emailName}@{_emailDomain}";
                headCoach.CanPublishPlaybook = true;
                _context.Users.Add(headCoach);
                HeadCoach = headCoach;
                return headCoach;
            }

            public User AddCoach(string firstName, string lastName, UserRole role, TeamUnit? coordinatorUnit, params string[] groupNames)
            {
                var coach = NewUser(firstName, lastName, role, coordinatorUnit);
                coach.CoachGroupAssignments = groupNames
                    .Select(name => new CoachGroupAssignment { PositionGroup = FindGroup(name) })
                    .ToList();
                _context.Users.Add(coach);
                return coach;
            }

            public User AddPlayer(int jerseyNumber, string firstName, string lastName, int grade, params string[] groupNames)
            {
                var player = NewUser(firstName, lastName, UserRole.Player, null);
                player.CanEditDepthChart = false;
                player.CanRecordAttendance = false;
                player.RosterEntry = new RosterEntry
                {
                    Team = Team,
                    JerseyNumber = jerseyNumber,
                    Grade = grade,
                    Status = RosterStatus.Active
                };
                player.PlayerGroupAssignments = groupNames
                    .Select(name => new PlayerGroupAssignment { PositionGroup = FindGroup(name) })
                    .ToList();

                _context.Users.Add(player);
                _playersByJersey[jerseyNumber] = player;
                return player;
            }

            public void AddDepthChartWeek(DateOnly monday, string label, Dictionary<string, int[]> chart)
            {
                var week = new DepthChartWeek
                {
                    Team = Team,
                    WeekStartDate = monday,
                    Label = label,
                    CreatedAt = _now,
                    Placements = new List<DepthChartPlacement>()
                };

                foreach (var slot in chart)
                {
                    var position = _context.Positions.Local.First(p => p.Team == Team && p.Code == slot.Key);

                    int rank = 0;
                    foreach (int jerseyNumber in slot.Value)
                    {
                        week.Placements.Add(new DepthChartPlacement
                        {
                            Team = Team,
                            Position = position,
                            Player = _playersByJersey[jerseyNumber],
                            Rank = ++rank
                        });
                    }
                }

                _context.DepthChartWeeks.Add(week);
            }

            public ScheduledEvent AddEvent(EventType type, string title, DateOnly date, TimeOnly start, TimeOnly? end, string location)
            {
                var scheduledEvent = new ScheduledEvent
                {
                    Team = Team,
                    EventType = type,
                    Title = title,
                    EventDate = date,
                    StartTime = start,
                    EndTime = end,
                    Location = location,
                    CreatedBy = HeadCoach,
                    CreatedAt = _now,
                    AttendanceRecords = new List<AttendanceRecord>()
                };

                _context.ScheduledEvents.Add(scheduledEvent);
                return scheduledEvent;
            }

            // Marks every player present at the event, except the jersey numbers given another status
            public void AddAttendance(ScheduledEvent scheduledEvent, Dictionary<int, AttendanceStatus> exceptionsByJersey)
            {
                // Recorded by the head coach on the evening of the event
                var date = scheduledEvent.EventDate;
                var recordedAt = new DateTime(date.Year, date.Month, date.Day, 23, 0, 0, DateTimeKind.Utc);

                foreach (var player in _playersByJersey)
                {
                    scheduledEvent.AttendanceRecords!.Add(new AttendanceRecord
                    {
                        Team = Team,
                        Player = player.Value,
                        Status = exceptionsByJersey.TryGetValue(player.Key, out var status) ? status : AttendanceStatus.Present,
                        RecordedBy = HeadCoach,
                        RecordedAt = recordedAt
                    });
                }
            }

            private User NewUser(string firstName, string lastName, UserRole role, TeamUnit? coordinatorUnit)
            {
                // firstname.lastname@team.example, with anything that is not a letter removed
                string emailName = new string($"{firstName}.{lastName}".ToLowerInvariant()
                    .Where(c => char.IsLetter(c) || c == '.')
                    .ToArray());

                return new User
                {
                    Team = Team,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = $"{emailName}@{_emailDomain}",
                    PasswordHash = _passwordHash,
                    Role = role,
                    CoordinatorUnit = coordinatorUnit,
                    CanPublishPlaybook = false,
                    CanEditDepthChart = true,
                    CanRecordAttendance = true,
                    MustChangePassword = false,
                    IsActive = true,
                    CreatedAt = _now
                };
            }

            private PositionGroup FindGroup(string name)
            {
                return _context.PositionGroups.Local.First(g => g.Team == Team && g.Name == name);
            }
        }
    }
}
