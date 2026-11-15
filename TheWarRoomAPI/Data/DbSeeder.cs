using Microsoft.AspNetCore.Identity;
using TheWarRoomAPI.Diagrams;
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

        // The main demo team: a full staff, a roster, two weeks of depth charts, a schedule with attendance,
        // a playbook with change requests, game film with tagged plays and cutups, and two weeks of study time
        private static void SeedLakeshoreLions(AppDbContext context, string passwordHash, DateTime now, DateOnly today, DateOnly thisMonday)
        {
            var lions = new DemoTeam(context, "Lakeshore Lions", "lakeshorelions.example", passwordHash, now);
            lions.Team.AttendanceTrackingEnabled = true;

            // ----- Coaching staff -----
            lions.AddHeadCoach("Marcus", "Bennett", "coach.bennett");

            var offenceCoordinator = lions.AddCoach("Elena", "Ruiz", UserRole.Coordinator, TeamUnit.Offence,
                "Quarterbacks", "Running Backs");
            offenceCoordinator.CanPublishPlaybook = true;   // the head coach has given her the publish right

            var defenceCoordinator = lions.AddCoach("Darnell", "Brooks", UserRole.Coordinator, TeamUnit.Defence);
            lions.AddCoach("Priya", "Nair", UserRole.Coordinator, TeamUnit.SpecialTeams, "Specialists");

            var receiversCoach = lions.AddCoach("Tom", "Gallagher", UserRole.PositionCoach, null, "Receivers", "Tight Ends");
            var lineCoach = lions.AddCoach("Sam", "Whitaker", UserRole.PositionCoach, null, "Offensive Line");
            var frontSevenCoach = lions.AddCoach("Andre", "Mitchell", UserRole.PositionCoach, null, "Defensive Line", "Linebackers");

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

            // ----- Playbook: a few approved plays, and change requests in each state -----
            // The base formations are already there (TeamDefaults). A play starts as a copy of its formation's
            // players; routes, blocks and motion are then added as [x, y] offsets from each player, in yards.

            var slantFlat = lions.AddPlay("Shotgun Spread", "Slant Flat", "Quick game. Read the flat defender: throw the slant if he widens.",
                diagram =>
                {
                    Route(diagram, "X", new[] { 0, 3.0 }, new[] { 6, 8.0 });
                    Route(diagram, "Z", new[] { 0, 3.0 }, new[] { -6, 8.0 });
                    Route(diagram, "H", new[] { -3, 2.0 }, new[] { -10, 3.0 });
                    Route(diagram, "Y", new[] { 3, 2.0 }, new[] { 10, 3.0 });
                    Block(diagram, "RB", new[] { 1.5, 2.5 });
                    foreach (var lineman in new[] { "LT", "LG", "C", "RG", "RT" })
                    {
                        Block(diagram, lineman, new[] { 0, 1.5 });
                    }
                });

            // Published directly by the offence coordinator, who has the publish right
            lions.AddPlay("Shotgun Spread", "Four Verticals", "Stretch the deep coverage. Inside receivers bend to the hashes.",
                diagram =>
                {
                    Route(diagram, "X", new[] { 0, 18.0 });
                    Route(diagram, "Z", new[] { 0, 18.0 });
                    Route(diagram, "H", new[] { 1, 19.0 });
                    Route(diagram, "Y", new[] { -1, 19.0 });
                    Route(diagram, "RB", new[] { 4, -1.0 }, new[] { 9, 2.0 });
                }, publishedBy: offenceCoordinator);

            lions.AddPlay("I-Form", "Power Right", "Gap scheme to the tight end side. The backside guard pulls and leads through the hole.",
                diagram =>
                {
                    Route(diagram, "RB", new[] { 1, 3.0 }, new[] { 5, 7.0 }, new[] { 7, 11.0 });
                    Block(diagram, "FB", new[] { 4, 4.5 });
                    Block(diagram, "LG", new[] { 2, -1.5 }, new[] { 6, 0.0 }, new[] { 7, 2.0 });
                    Block(diagram, "TE", new[] { 1, 2.0 });
                    foreach (var lineman in new[] { "LT", "C", "RG", "RT" })
                    {
                        Block(diagram, lineman, new[] { -1, 1.5 });
                    }
                });

            // Defence: the four linemen rush and the other seven drop into zones
            var coverThree = lions.AddPlay("4-3", "Cover 3", "Three deep, four under. Corners and the free safety take the deep thirds.",
                diagram =>
                {
                    Zone(diagram, "LCB", new[] { 0, 10.0 });
                    Zone(diagram, "RCB", new[] { 0, 10.0 });
                    Zone(diagram, "FS", new[] { 6, 5.0 });
                    Zone(diagram, "SS", new[] { 7, -2.5 });
                    Zone(diagram, "SLB", new[] { -1, 3.5 });
                    Zone(diagram, "MLB", new[] { -2.5, 4.0 });
                    Zone(diagram, "WLB", new[] { -6, 3.0 });
                    foreach (var rusher in new[] { "LE", "DT", "NT", "RE" })
                    {
                        Rush(diagram, rusher, new[] { 0, -4.0 });
                    }
                });

            // Defence: man coverage. The offence is drawn in so that each defender's line ends on his man.
            lions.AddPlay("4-3", "Cover 1 Blitz", "Man coverage with the free safety deep in the middle. The left end slants inside and the strong-side linebacker comes off the edge behind him.",
                diagram =>
                {
                    Opponent(diagram, "QB", 0, -2.5);
                    Opponent(diagram, "RB", 0, -6.5);
                    Opponent(diagram, "X", -22, -1);
                    Opponent(diagram, "Y", -13, -2);
                    Opponent(diagram, "TE", 8.5, -1);
                    Opponent(diagram, "Z", 22, -1);

                    Man(diagram, "RCB", -2, -7);
                    Man(diagram, "LCB", 2, -7);
                    Man(diagram, "WLB", -7, -7);
                    Man(diagram, "SS", 0.5, -12);
                    Man(diagram, "MLB", 0, -11.5);
                    Zone(diagram, "FS", new[] { 6, 4.0 });
                    Rush(diagram, "SLB", new[] { 0.8, -4.0 }, new[] { 0, -9.0 });
                    Rush(diagram, "LE", new[] { -1.75, -4.5 });
                    foreach (var rusher in new[] { "DT", "NT", "RE" })
                    {
                        Rush(diagram, rusher, new[] { 0, -4.0 });
                    }
                });

            context.SaveChanges();

            // Waiting for the head coach: the receivers coach wants deeper slants (players still see the version above)
            lions.AddUpdateRequest(slantFlat, receiversCoach, "Slant Flat", slantFlat.Notes, diagram =>
            {
                ReplaceRoute(diagram, "X", new[] { 0, 5.0 }, new[] { 8, 11.0 });
                ReplaceRoute(diagram, "Z", new[] { 0, 5.0 }, new[] { -8, 11.0 });
                diagram.Drawings.Add(new DiagramDrawing { Id = "t1", Type = "text", X = -25, Y = 16, Text = "Slants break at 5" });
            });

            // A new play from the offensive line coach, in two rounds. The head coach rejected the first drawing
            // with a note; the coach revised it and sent it again, and that second request is waiting now.
            var counterLeftFirst = lions.AddNewPlayRequest("I-Form", lineCoach, "Counter Left", "Misdirection off Power. Both backs step right, then the ball goes left.",
                diagram =>
                {
                    Route(diagram, "RB", new[] { 2, 1.0 }, new[] { -4, 6.0 }, new[] { -7, 11.0 });
                    Block(diagram, "FB", new[] { -4, 4.5 });
                    Block(diagram, "RG", new[] { -2, -1.5 }, new[] { -6, 0.0 }, new[] { -7, 2.0 });
                    Block(diagram, "RT", new[] { -2, -2.5 }, new[] { -9, -1.0 }, new[] { -10, 2.0 });
                    foreach (var lineman in new[] { "LT", "LG", "C", "TE" })
                    {
                        Block(diagram, lineman, new[] { 1, 1.5 });
                    }
                });
            counterLeftFirst.RequestedAt = now.AddDays(-2);
            counterLeftFirst.Status = ApprovalStatus.Rejected;
            counterLeftFirst.ReviewedBy = lions.HeadCoach;
            counterLeftFirst.ReviewedAt = now.AddDays(-1);
            counterLeftFirst.ReviewNote = "Pulling the tackle as well leaves the back side open. Pull the right guard only and send it again.";
            counterLeftFirst.DecisionSeenAt = now.AddHours(-20);   // the coach has read it

            var counterLeft = lions.AddNewPlayRequest("I-Form", lineCoach, "Counter Left", "Misdirection off Power. Both backs step right, then the ball goes left.",
                diagram =>
                {
                    Route(diagram, "RB", new[] { 2, 1.0 }, new[] { -4, 6.0 }, new[] { -7, 11.0 });
                    Block(diagram, "FB", new[] { -4, 4.5 });
                    Block(diagram, "RG", new[] { -2, -1.5 }, new[] { -6, 0.0 }, new[] { -7, 2.0 });
                    foreach (var lineman in new[] { "LT", "LG", "C", "RT", "TE" })
                    {
                        Block(diagram, lineman, new[] { 1, 1.5 });
                    }
                });
            counterLeft.RequestedAt = now.AddHours(-18);
            counterLeft.ResubmittedFrom = counterLeftFirst;

            // Rejected, and not read yet: a play the receivers coach suggested. The coach is told when signing in,
            // and can revise the drawing with the head coach's note beside it and send it again.
            var mesh = lions.AddNewPlayRequest("Shotgun Spread", receiversCoach, "Mesh", "Two shallow crossers under a post and a dig. Throw to the crosser who comes open.",
                diagram =>
                {
                    Route(diagram, "H", new[] { 2, 4.0 }, new[] { 20, 7.0 });
                    Route(diagram, "Y", new[] { -2, 4.0 }, new[] { -20, 7.0 });
                    Route(diagram, "X", new[] { 0, 10.0 }, new[] { 6, 10.0 });
                    Route(diagram, "Z", new[] { 0, 11.0 }, new[] { -5, 16.0 });
                    Route(diagram, "RB", new[] { 4, -1.0 }, new[] { 9, 2.0 });
                    foreach (var lineman in new[] { "LT", "LG", "C", "RG", "RT" })
                    {
                        Block(diagram, lineman, new[] { 0, 1.5 });
                    }
                });
            mesh.RequestedAt = now.AddDays(-1);
            mesh.Status = ApprovalStatus.Rejected;
            mesh.ReviewedBy = lions.HeadCoach;
            mesh.ReviewedAt = now.AddHours(-3);
            mesh.ReviewNote = "The two crossers meet at the same depth. Run H at six yards with Y underneath at four, and send it again.";

            // Rejected, and not read yet: the head coach turned this change down, so Cover 3 is unchanged
            var rejected = lions.AddUpdateRequest(coverThree, frontSevenCoach, "Cover 3", coverThree.Notes, diagram =>
            {
                diagram.Players.First(p => p.Label == "MLB").Paths.Clear();
                Zone(diagram, "MLB", new[] { 0, 8.0 });
            });
            rejected.Status = ApprovalStatus.Rejected;
            rejected.ReviewedBy = lions.HeadCoach;
            rejected.ReviewedAt = now;
            rejected.ReviewNote = "Keep the middle linebacker's drop short for now. Bring it back after the bye week.";

            context.SaveChanges();

            // ----- Film: four games, their plays cut and tagged, and cutups shared in different ways -----
            // The video records have no file behind them: they are there so plays can be tagged and charted,
            // and cutups built and shared, before any film has been uploaded.
            var knights = lions.AddOpponent("Northgate Knights");    // this week's opponent
            var raiders = lions.AddOpponent("Riverside Raiders");    // last week's opponent
            var eagles = lions.AddOpponent("Eastview Eagles");
            var ourselves = lions.OwnTeamOnFilm;

            var lastFriday = thisMonday.AddDays(-3);
            var theFridayBefore = thisMonday.AddDays(-10);

            var atRiverside = lions.AddGameFilm(lastFriday, home: raiders, away: ourselves, "Week 7", offenceCoordinator);
            var knightsAtEastview = lions.AddGameFilm(lastFriday, home: eagles, away: knights, "Scout film", defenceCoordinator);
            var raidersAtNorthgate = lions.AddGameFilm(theFridayBefore, home: knights, away: raiders, "Scout film", defenceCoordinator);
            var eastviewAtHome = lions.AddGameFilm(theFridayBefore, home: ourselves, away: eagles, "Week 6", offenceCoordinator);

            // Each line: who has the ball, the down, the distances to go, then how many runs and how many passes.
            // Northgate's two games together give 12 runs and 8 passes on first down: a 60% run tendency from 20 plays.
            lions.AddPlays(knightsAtEastview, knights, 1, new[] { 10 }, runs: 6, passes: 4);
            lions.AddPlays(knightsAtEastview, knights, 2, new[] { 2, 3 }, runs: 2, passes: 0);
            lions.AddPlays(knightsAtEastview, knights, 2, new[] { 4, 5, 6 }, runs: 2, passes: 2);
            lions.AddPlays(knightsAtEastview, knights, 2, new[] { 7, 8, 9, 10 }, runs: 1, passes: 3);
            lions.AddPlays(knightsAtEastview, knights, 3, new[] { 1, 2 }, runs: 2, passes: 1);
            lions.AddPlays(knightsAtEastview, knights, 3, new[] { 4, 5 }, runs: 0, passes: 2);
            lions.AddPlays(knightsAtEastview, knights, 3, new[] { 7, 8, 9 }, runs: 0, passes: 3);
            lions.AddPlays(knightsAtEastview, knights, 3, new[] { 12 }, runs: 0, passes: 1);
            lions.AddPlays(knightsAtEastview, knights, 4, new[] { 1 }, runs: 1, passes: 0);
            lions.AddPlays(knightsAtEastview, eagles, 1, new[] { 10 }, runs: 4, passes: 6);
            lions.AddPlays(knightsAtEastview, eagles, 2, new[] { 3, 6, 9 }, runs: 3, passes: 4);
            lions.AddPlays(knightsAtEastview, eagles, 3, new[] { 2, 5, 8, 11 }, runs: 1, passes: 5);

            lions.AddPlays(raidersAtNorthgate, knights, 1, new[] { 10 }, runs: 6, passes: 4);
            lions.AddPlays(raidersAtNorthgate, knights, 2, new[] { 1, 3 }, runs: 3, passes: 1);
            lions.AddPlays(raidersAtNorthgate, knights, 2, new[] { 4, 5, 6 }, runs: 2, passes: 2);
            lions.AddPlays(raidersAtNorthgate, knights, 2, new[] { 7, 8, 10 }, runs: 2, passes: 3);
            lions.AddPlays(raidersAtNorthgate, knights, 2, new[] { 12, 15 }, runs: 0, passes: 2);
            lions.AddPlays(raidersAtNorthgate, knights, 3, new[] { 1, 2, 3 }, runs: 2, passes: 1);
            lions.AddPlays(raidersAtNorthgate, knights, 3, new[] { 4, 5, 6 }, runs: 1, passes: 3);
            lions.AddPlays(raidersAtNorthgate, knights, 3, new[] { 7, 9, 10 }, runs: 1, passes: 3);
            lions.AddPlays(raidersAtNorthgate, knights, 3, new[] { 13, 17 }, runs: 0, passes: 2);
            lions.AddPlays(raidersAtNorthgate, knights, 4, new[] { 1, 2 }, runs: 1, passes: 1);
            lions.AddPlays(raidersAtNorthgate, raiders, 1, new[] { 10 }, runs: 7, passes: 3);
            lions.AddPlays(raidersAtNorthgate, raiders, 2, new[] { 2, 5, 8 }, runs: 5, passes: 3);
            lions.AddPlays(raidersAtNorthgate, raiders, 3, new[] { 1, 4, 7, 10 }, runs: 3, passes: 4);

            lions.AddPlays(atRiverside, ourselves, 1, new[] { 10 }, runs: 9, passes: 11);
            lions.AddPlays(atRiverside, ourselves, 2, new[] { 2, 4, 6, 8, 10 }, runs: 6, passes: 8);
            lions.AddPlays(atRiverside, ourselves, 3, new[] { 1, 3, 5, 7, 9, 12 }, runs: 3, passes: 8);
            lions.AddPlays(atRiverside, ourselves, 4, new[] { 1, 2 }, runs: 1, passes: 1);
            lions.AddPlays(atRiverside, raiders, 1, new[] { 10 }, runs: 8, passes: 4);
            lions.AddPlays(atRiverside, raiders, 2, new[] { 3, 5, 7, 9 }, runs: 5, passes: 4);
            lions.AddPlays(atRiverside, raiders, 3, new[] { 2, 4, 6, 8, 11 }, runs: 2, passes: 6);

            lions.AddPlays(eastviewAtHome, ourselves, 1, new[] { 10 }, runs: 10, passes: 9);
            lions.AddPlays(eastviewAtHome, ourselves, 2, new[] { 1, 3, 5, 7, 9 }, runs: 7, passes: 6);
            lions.AddPlays(eastviewAtHome, ourselves, 3, new[] { 2, 4, 6, 8, 10 }, runs: 3, passes: 7);
            lions.AddPlays(eastviewAtHome, eagles, 1, new[] { 10 }, runs: 5, passes: 7);
            lions.AddPlays(eastviewAtHome, eagles, 2, new[] { 4, 6, 8 }, runs: 3, passes: 5);
            lions.AddPlays(eastviewAtHome, eagles, 3, new[] { 3, 6, 9, 13 }, runs: 1, passes: 6);

            // The plays are spread over each video in a mixed order, the way a game goes back and forth
            int gameNumber = 0;
            foreach (var video in new[] { atRiverside, knightsAtEastview, raidersAtNorthgate, eastviewAtHome })
            {
                lions.AddPlayWithoutTag(video, "Opening kickoff");
                lions.AddPlayWithoutTag(video, "Punt");
                lions.AddPlayWithoutTag(video, "Field goal");
                lions.LayOutPlays(video, ++gameNumber);
            }

            // Cutups for this week's game against Northgate, each shared a different way
            var knightsFirstDowns = lions.AddCutup("Northgate offence on first down",
                "What Northgate does on 1st and 10. They run six times out of ten.", thisMonday, defenceCoordinator,
                lions.Plays(knights, down: 1).Take(10));
            lions.ShareWithTeam(knightsFirstDowns, defenceCoordinator);

            var knightsThirdDowns = lions.AddCutup("Northgate passing on third down",
                "They throw on third down unless it is short. Watch where the receivers line up.", thisMonday, defenceCoordinator,
                lions.Plays(knights, down: 3, playType: PlayType.Pass));
            lions.ShareWithGroups(knightsThirdDowns, defenceCoordinator, "Linebackers", "Defensive Backs");

            var ourPassGame = lions.AddCutup("Our pass plays at Riverside",
                "Every pass we threw on first and second down last week.", thisMonday, receiversCoach,
                lions.Plays(ourselves, playType: PlayType.Pass, video: atRiverside).Where(c => c.Tag!.Down <= 2));
            lions.ShareWithGroups(ourPassGame, receiversCoach, "Receivers", "Tight Ends");
            lions.ShareWithPlayers(ourPassGame, receiversCoach, 7);   // and the starting quarterback

            var thirdDownDecisions = lions.AddCutup("Jalen: third down decisions",
                "Our third downs from the last two games.", thisMonday, offenceCoordinator,
                lions.Plays(ourselves, down: 3));
            lions.ShareWithPlayers(thirdDownDecisions, offenceCoordinator, 7);

            // Not shared with anyone yet, so only coaches see it
            lions.AddCutup("Riverside short yardage (in progress)", null, null, offenceCoordinator,
                lions.Plays(raiders).Where(c => c.Tag!.Distance <= 3));

            context.SaveChanges();

            // ----- Study time: two weeks of sign-ins, playbook reading and film watching by the players -----
            // The film time is counted on the cutup that is shared with the whole team.
            lions.AddStudyHistory(now, knightsFirstDowns);

            context.SaveChanges();
        }

        // ----- Small helpers for writing plays -----

        private static void Route(DiagramDocument diagram, string label, params double[][] points)
        {
            AddPath(diagram, label, "route", points);
        }

        private static void Block(DiagramDocument diagram, string label, params double[][] points)
        {
            AddPath(diagram, label, "block", points);
        }

        // What a defender does: rush the passer, drop into a zone, or cover a man.
        // A man line is one straight step, from the defender to the player he covers.
        private static void Rush(DiagramDocument diagram, string label, params double[][] points)
        {
            AddPath(diagram, label, "rush", points);
        }

        private static void Zone(DiagramDocument diagram, string label, params double[][] points)
        {
            AddPath(diagram, label, "zone", points);
        }

        private static void Man(DiagramDocument diagram, string label, double acrossToTheMan, double upToTheMan)
        {
            AddPath(diagram, label, "man", new[] { new[] { acrossToTheMan, upToTheMan } });
        }

        // A player of the other side, drawn for reference
        private static void Opponent(DiagramDocument diagram, string label, double x, double y)
        {
            diagram.Players.Add(new DiagramPlayer
            {
                Id = "opp-" + label.ToLowerInvariant(),
                Team = "opp",
                Label = label,
                Shape = "circle",
                X = x,
                Y = y
            });
        }

        private static void ReplaceRoute(DiagramDocument diagram, string label, params double[][] points)
        {
            diagram.Players.First(p => p.Label == label).Paths.Clear();
            AddPath(diagram, label, "route", points);
        }

        private static void AddPath(DiagramDocument diagram, string label, string kind, double[][] points)
        {
            diagram.Players.First(p => p.Label == label).Paths.Add(new DiagramPath { Kind = kind, Pts = points.ToList() });
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

                // The same groups, positions and playbooks a newly registered team gets
                TeamDefaults.AddTo(Team, context, now);
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

            // An approved play, drawn on top of one of the team's formations.
            // When publishedBy is given, the history shows that coach publishing it directly.
            public Play AddPlay(string formationName, string name, string notes, Action<DiagramDocument> draw, User? publishedBy = null)
            {
                var formation = FindFormation(formationName);
                var diagram = DiagramValidator.Parse(formation.DiagramJson);
                draw(diagram);

                var play = new Play
                {
                    Team = Team,
                    Formation = formation,
                    Name = name,
                    DiagramJson = DiagramValidator.Normalize(DiagramValidator.Serialize(diagram), diagram.Unit, isFormation: false, rules: Team.Rules),
                    Notes = notes,
                    Version = 1,
                    UpdatedAt = _now
                };
                _context.Plays.Add(play);

                if (publishedBy != null)
                {
                    _context.ApprovalRequests.Add(new ApprovalRequest
                    {
                        Team = Team,
                        Playbook = formation.Playbook,
                        TargetType = PlaybookTargetType.Play,
                        Action = PlaybookChangeAction.Create,
                        Formation = formation,
                        Play = play,
                        TargetName = name,
                        ProposedName = name,
                        ProposedJson = play.DiagramJson,
                        ProposedNotes = notes,
                        Status = ApprovalStatus.Approved,
                        AutoApproved = true,
                        RequestedBy = publishedBy,
                        RequestedAt = _now,
                        ReviewedBy = publishedBy,
                        ReviewedAt = _now
                    });
                }

                return play;
            }

            // A pending request to change an existing play
            public ApprovalRequest AddUpdateRequest(Play play, User requestedBy, string name, string? notes, Action<DiagramDocument> change)
            {
                var diagram = DiagramValidator.Parse(play.DiagramJson);
                change(diagram);

                var request = new ApprovalRequest
                {
                    Team = Team,
                    Playbook = play.Formation!.Playbook,
                    TargetType = PlaybookTargetType.Play,
                    Action = PlaybookChangeAction.Update,
                    Formation = play.Formation,
                    Play = play,
                    BaseVersion = play.Version,
                    TargetName = play.Name,
                    ProposedName = name,
                    ProposedJson = DiagramValidator.Normalize(DiagramValidator.Serialize(diagram), diagram.Unit, isFormation: false, rules: Team.Rules),
                    ProposedNotes = notes,
                    Status = ApprovalStatus.Pending,
                    RequestedBy = requestedBy,
                    RequestedAt = _now
                };
                _context.ApprovalRequests.Add(request);
                return request;
            }

            // A pending request for a play that does not exist yet
            public ApprovalRequest AddNewPlayRequest(string formationName, User requestedBy, string name, string notes, Action<DiagramDocument> draw)
            {
                var formation = FindFormation(formationName);
                var diagram = DiagramValidator.Parse(formation.DiagramJson);
                draw(diagram);

                var request = new ApprovalRequest
                {
                    Team = Team,
                    Playbook = formation.Playbook,
                    TargetType = PlaybookTargetType.Play,
                    Action = PlaybookChangeAction.Create,
                    Formation = formation,
                    TargetName = name,
                    ProposedName = name,
                    ProposedJson = DiagramValidator.Normalize(DiagramValidator.Serialize(diagram), diagram.Unit, isFormation: false, rules: Team.Rules),
                    ProposedNotes = notes,
                    Status = ApprovalStatus.Pending,
                    RequestedBy = requestedBy,
                    RequestedAt = _now
                };
                _context.ApprovalRequests.Add(request);
                return request;
            }

            // ----- Film -----

            // Plays that have been described for a video but not yet placed on its timeline (see LayOutPlays)
            private readonly Dictionary<Video, List<Clip>> _playsToLayOut = new Dictionary<Video, List<Clip>>();

            // The team's own entry among the teams on film (TeamDefaults adds it)
            public ScoutedTeam OwnTeamOnFilm => _context.ScoutedTeams.Local.First(t => t.Team == Team && t.IsOwnTeam);

            public ScoutedTeam AddOpponent(string name)
            {
                var opponent = new ScoutedTeam { Team = Team, Name = name };
                _context.ScoutedTeams.Add(opponent);
                return opponent;
            }

            // A game on film with one video of the whole game. The video is ready to be cut, but has no file.
            public Video AddGameFilm(DateOnly date, ScoutedTeam home, ScoutedTeam away, string label, User uploadedBy)
            {
                var game = new Game
                {
                    Team = Team,
                    GameDate = date,
                    HomeTeam = home,
                    AwayTeam = away,
                    Label = label,
                    CreatedBy = uploadedBy,
                    CreatedAt = _now
                };

                var video = new Video
                {
                    Team = Team,
                    Game = game,
                    Title = "Full game",
                    Status = VideoStatus.Ready,
                    DurationMs = (1 * 60 + 52) * 60 * 1000,   // 1 hour 52 minutes
                    UploadedBy = uploadedBy,
                    CreatedAt = _now,
                    Clips = new List<Clip>()
                };

                _context.Videos.Add(video);   // the game is saved with it through the navigation property
                _playsToLayOut[video] = new List<Clip>();
                return video;
            }

            // Tagged plays for one team at one down: first the runs, then the passes,
            // with the distance to go taken in turn from the list
            public void AddPlays(Video video, ScoutedTeam offence, int down, int[] distances, int runs, int passes)
            {
                for (int i = 0; i < runs + passes; i++)
                {
                    _playsToLayOut[video].Add(new Clip
                    {
                        Team = Team,
                        CreatedBy = video.UploadedBy,
                        CreatedAt = _now,
                        UpdatedAt = _now,
                        Tag = new PlayTag
                        {
                            Team = Team,
                            PlayType = i < runs ? PlayType.Run : PlayType.Pass,
                            Down = down,
                            Distance = distances[i % distances.Length],
                            OffenceTeam = offence
                        }
                    });
                }
            }

            // A play that is cut but not tagged, so it is left out of the tendency charts
            public void AddPlayWithoutTag(Video video, string title)
            {
                _playsToLayOut[video].Add(new Clip
                {
                    Team = Team,
                    Title = title,
                    CreatedBy = video.UploadedBy,
                    CreatedAt = _now,
                    UpdatedAt = _now
                });
            }

            // Puts a video's plays on its timeline in a mixed order.
            // The same number always gives the same order, so the demo data is the same on every run.
            public void LayOutPlays(Video video, int orderNumber)
            {
                var random = new Random(orderNumber);
                var plays = _playsToLayOut[video].OrderBy(_ => random.Next()).ToList();

                int startMs = 45_000;   // the first play starts 45 seconds in
                foreach (var play in plays)
                {
                    play.StartMs = startMs;
                    play.EndMs = startMs + random.Next(6, 12) * 1000;   // a play lasts 6 to 11 seconds
                    video.Clips!.Add(play);

                    startMs = play.EndMs + random.Next(25, 41) * 1000;   // and the next one starts 25 to 40 seconds later
                }
            }

            // The tagged plays on film where this team has the ball, in the order they happened.
            // The other arguments narrow the list.
            public IEnumerable<Clip> Plays(ScoutedTeam offence, int? down = null, PlayType? playType = null, Video? video = null)
            {
                return _context.Clips.Local
                    .Where(c => c.Team == Team && c.Tag != null && c.Tag.OffenceTeam == offence)
                    .Where(c => down == null || c.Tag!.Down == down)
                    .Where(c => playType == null || c.Tag!.PlayType == playType)
                    .Where(c => video == null || c.Video == video)
                    .OrderBy(c => c.Video!.Game!.GameDate)
                    .ThenBy(c => c.StartMs);
            }

            // A draft cutup made of the given plays, in the order given
            public Cutup AddCutup(string name, string? description, DateOnly? forWeekOf, User createdBy, IEnumerable<Clip> clips)
            {
                var cutup = new Cutup
                {
                    Team = Team,
                    Name = name,
                    Description = description,
                    ForWeekOf = forWeekOf,
                    Status = CutupStatus.Draft,
                    CreatedBy = createdBy,
                    CreatedAt = _now,
                    UpdatedAt = _now,
                    Clips = new List<CutupClip>(),
                    Assignments = new List<FilmAssignment>()
                };

                int position = 0;
                foreach (var clip in clips)
                {
                    cutup.Clips.Add(new CutupClip
                    {
                        Team = Team,
                        Position = ++position,
                        Clip = clip,
                        Video = clip.Video,
                        StartMs = clip.StartMs,
                        EndMs = clip.EndMs,
                        Title = clip.Title
                    });
                }

                _context.Cutups.Add(cutup);
                return cutup;
            }

            public void ShareWithTeam(Cutup cutup, User sharedBy)
            {
                cutup.Assignments!.Add(new FilmAssignment { Audience = FilmAudience.Team, AssignedBy = sharedBy, AssignedAt = _now });
            }

            public void ShareWithGroups(Cutup cutup, User sharedBy, params string[] groupNames)
            {
                foreach (var groupName in groupNames)
                {
                    cutup.Assignments!.Add(new FilmAssignment
                    {
                        Audience = FilmAudience.Group,
                        PositionGroup = FindGroup(groupName),
                        AssignedBy = sharedBy,
                        AssignedAt = _now
                    });
                }
            }

            public void ShareWithPlayers(Cutup cutup, User sharedBy, params int[] jerseyNumbers)
            {
                foreach (int jerseyNumber in jerseyNumbers)
                {
                    cutup.Assignments!.Add(new FilmAssignment
                    {
                        Audience = FilmAudience.Player,
                        Player = _playersByJersey[jerseyNumber],
                        AssignedBy = sharedBy,
                        AssignedAt = _now
                    });
                }
            }

            // ----- Study time -----

            // Sign-ins and viewing time for the last two weeks, different for every player and every day.
            // They follow from the jersey number and the day, so the demo data is the same on every run.
            public void AddStudyHistory(DateTime now, Cutup filmForTheTeam)
            {
                foreach (var entry in _playersByJersey)
                {
                    int jerseyNumber = entry.Key;
                    var player = entry.Value;

                    // A player who has not signed in for the first time yet has no history
                    if (player.MustChangePassword)
                    {
                        continue;
                    }

                    // The playbook of the first unit the player belongs to
                    var unit = player.PlayerGroupAssignments!.First().PositionGroup!.Unit;
                    var playbook = _context.Playbooks.Local.First(p => p.Team == Team && p.Unit == unit);

                    for (int daysAgo = 13; daysAgo >= 1; daysAgo--)
                    {
                        var random = new Random(jerseyNumber * 100 + daysAgo);

                        // Nobody signs in every day: about three days out of five
                        if (random.Next(5) < 2)
                        {
                            continue;
                        }

                        // Signed in for 15 to 55 minutes, some hours before this time of day
                        int loginMinutes = random.Next(15, 56);
                        var startedAt = now.AddDays(-daysAgo).AddMinutes(-random.Next(0, 300));

                        var login = new LoginSession
                        {
                            User = player,
                            Token = Guid.NewGuid().ToString("N"),
                            StartedAt = startedAt,
                            LastSeenAt = startedAt.AddMinutes(loginMinutes),
                            EndedAt = startedAt.AddMinutes(loginMinutes)
                        };
                        _context.LoginSessions.Add(login);

                        // Part of that time went to the playbook and part to film; the rest was spent elsewhere on the site
                        int playbookMinutes = random.Next(10) < 7 ? random.Next(3, loginMinutes / 3 + 1) : 0;
                        int filmMinutes = random.Next(10) < 6 ? random.Next(4, loginMinutes / 2 + 1) : 0;

                        if (playbookMinutes > 0)
                        {
                            AddViewing(login, ViewingKind.Playbook, startedAt.AddMinutes(1), playbookMinutes, playbook, null);
                        }
                        if (filmMinutes > 0)
                        {
                            AddViewing(login, ViewingKind.Film, startedAt.AddMinutes(2 + playbookMinutes), filmMinutes, null, filmForTheTeam);
                        }
                    }
                }
            }

            private void AddViewing(LoginSession login, ViewingKind kind, DateTime startedAt, int minutes, Playbook? playbook, Cutup? cutup)
            {
                _context.ViewingSessions.Add(new ViewingSession
                {
                    Team = Team,
                    User = login.User,
                    Kind = kind,
                    Playbook = playbook,
                    Cutup = cutup,
                    PageKey = Guid.NewGuid().ToString("N"),
                    LoginSession = login,
                    StartedAt = startedAt,
                    LastCreditAt = startedAt.AddMinutes(minutes),
                    ActiveMs = minutes * 60 * 1000
                });
            }

            private Formation FindFormation(string name)
            {
                return _context.Formations.Local.First(f => f.Team == Team && f.Name == name);
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
