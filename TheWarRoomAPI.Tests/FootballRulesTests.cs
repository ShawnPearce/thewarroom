using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The game a team plays, American (11 a side) or Canadian (12 a side): who chooses it,
    // and what it changes on the depth chart and in the playbooks
    public class FootballRulesTests : IDisposable
    {
        private const string TeamName = "Test Tigers";

        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // The head coach changes the game in the team's settings.
        // Returns the head coach as the next request sees them, with the new game.
        private async Task<CurrentUser> SwitchAsync(CurrentUser headCoach, FootballRules rules)
        {
            using (var context = _world.NewContext())
            {
                Assert.True(await _world.Teams(context).UpdateAsync(headCoach, new UpdateTeamDto { Name = TeamName, Rules = rules }));
            }

            return await _world.AsAsync(headCoach.Id);
        }

        // The codes of the positions the depth chart of a week shows
        private async Task<List<string>> ChartCodesAsync(CurrentUser user, int weekId)
        {
            using var context = _world.NewContext();
            var chart = await _world.DepthChart(context).GetWeekAsync(user, weekId);
            return chart!.Groups.SelectMany(g => g.Positions).Select(p => p.Code).ToList();
        }

        private async Task<int> PlaybookIdAsync(CurrentUser teamMember, TeamUnit unit, FootballRules rules)
        {
            using var context = _world.NewContext();
            return await context.Playbooks
                .Where(p => p.TeamId == teamMember.TeamId && p.Unit == unit && p.Rules == rules)
                .Select(p => p.Id)
                .SingleAsync();
        }

        // ---------- Choosing the game ----------

        [Fact]
        public async Task ANewTeam_PlaysAmericanFootball_ElevenASide()
        {
            var headCoach = await _world.AddTeamAsync();

            using var context = _world.NewContext();
            var team = await _world.Teams(context).GetAsync(headCoach);

            Assert.Equal(FootballRules.American, team!.Rules);
            Assert.Equal(11, team.PlayersPerSide);
            Assert.Equal(FootballRules.American, headCoach.Rules);
        }

        [Fact]
        public async Task OnlyTheHeadCoach_ChoosesTheGame()
        {
            var headCoach = await _world.AddTeamAsync();
            var coordinator = await _world.AddCoachAsync(headCoach, UserRole.Coordinator, TeamUnit.Offence);
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var change = new UpdateTeamDto { Name = TeamName, Rules = FootballRules.Canadian };

            using (var context = _world.NewContext())
            {
                var service = _world.Teams(context);
                await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(coordinator, change));
                await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAsync(player, change));
            }

            using (var context = _world.NewContext())
            {
                var service = _world.Teams(context);
                Assert.True(await service.UpdateAsync(headCoach, change));

                var team = await service.GetAsync(headCoach);
                Assert.Equal(FootballRules.Canadian, team!.Rules);
                Assert.Equal(12, team.PlayersPerSide);
            }

            // Everyone on the team is in the new game from their next request on
            Assert.Equal(FootballRules.Canadian, (await _world.AsAsync(player.Id)).Rules);
        }

        [Fact]
        public async Task SavingTheSettingsWithoutNamingAGame_KeepsTheGame()
        {
            var headCoach = await SwitchAsync(await _world.AddTeamAsync(), FootballRules.Canadian);

            using var context = _world.NewContext();
            var service = _world.Teams(context);
            Assert.True(await service.UpdateAsync(headCoach, new UpdateTeamDto { Name = "Renamed Tigers", AttendanceTrackingEnabled = true }));

            var team = await service.GetAsync(headCoach);
            Assert.Equal("Renamed Tigers", team!.Name);
            Assert.Equal(FootballRules.Canadian, team.Rules);
        }

        [Fact]
        public async Task ATeam_CanRegisterAsACanadianTeam()
        {
            LoginResultDto registered;
            using (var context = _world.NewContext())
            {
                registered = await _world.Teams(context).RegisterAsync(new RegisterTeamDto
                {
                    TeamName = "Northern Lights",
                    Rules = FootballRules.Canadian,
                    FirstName = "Casey",
                    LastName = "Coach",
                    Email = "casey@example.com",
                    Password = "Long-Enough-1"
                });
            }

            var headCoach = await _world.AsAsync(registered.UserId);
            Assert.Equal(FootballRules.Canadian, headCoach.Rules);

            // The team starts with the Canadian positions and playbooks only: there is nothing American to put away
            using var checkContext = _world.NewContext();
            var codes = await checkContext.Positions.Where(p => p.TeamId == headCoach.TeamId).Select(p => p.Code).ToListAsync();
            Assert.Contains("W", codes);
            Assert.Contains("LHB", codes);
            Assert.DoesNotContain("TE", codes);
            Assert.DoesNotContain("FS", codes);
            Assert.False(await checkContext.PositionGroups.AnyAsync(g => g.TeamId == headCoach.TeamId && g.Name == "Tight Ends"));

            var playbooks = await checkContext.Playbooks.Where(p => p.TeamId == headCoach.TeamId).ToListAsync();
            Assert.Equal(3, playbooks.Count);
            Assert.All(playbooks, p => Assert.Equal(FootballRules.Canadian, p.Rules));
        }

        // ---------- The depth chart ----------

        [Fact]
        public async Task TakingUpCanadianFootball_ChangesThePositionsOnTheDepthChart()
        {
            var headCoach = await _world.AddTeamAsync();
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            var american = await ChartCodesAsync(headCoach, weekId);
            Assert.Contains("TE", american);
            Assert.Contains("FS", american);
            Assert.DoesNotContain("W", american);
            Assert.DoesNotContain("LHB", american);

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            // The twelfth player: two more slotbacks on offence; two halfbacks and one safety instead of two safeties on defence
            var canadian = await ChartCodesAsync(headCoach, weekId);
            Assert.Contains("W", canadian);
            Assert.Contains("R", canadian);
            Assert.Contains("LHB", canadian);
            Assert.Contains("RHB", canadian);
            Assert.Contains("S", canadian);
            Assert.DoesNotContain("TE", canadian);
            Assert.DoesNotContain("FS", canadian);
            Assert.DoesNotContain("SS", canadian);

            // What both games have is still there
            Assert.Contains("QB", canadian);
            Assert.Contains("MLB", canadian);
            Assert.Contains("K", canadian);

            using var context = _world.NewContext();
            var chart = await _world.DepthChart(context).GetWeekAsync(headCoach, weekId);
            Assert.Equal(FootballRules.Canadian, chart!.Rules);
            Assert.Equal(12, chart.PlayersPerSide);

            // A group with nothing left to show is left off the chart
            Assert.DoesNotContain(chart.Groups, g => g.GroupName == "Tight Ends");
        }

        [Fact]
        public async Task PositionsOfTheOtherGame_ArePutAway_NotDeleted()
        {
            var headCoach = await SwitchAsync(await _world.AddTeamAsync(), FootballRules.Canadian);

            using var context = _world.NewContext();
            var groups = await _world.Groups(context).GetGroupsAsync(headCoach);
            var positions = groups.SelectMany(g => g.Positions).ToList();

            // The tight end is still the team's, marked as American and put away
            var tightEnd = positions.Single(p => p.Code == "TE");
            Assert.Equal(FootballRules.American, tightEnd.Rules);
            Assert.True(tightEnd.IsPutAway);

            // A slotback is Canadian and in use; the quarterback belongs to both games
            var slotback = positions.Single(p => p.Code == "W");
            Assert.Equal(FootballRules.Canadian, slotback.Rules);
            Assert.False(slotback.IsPutAway);

            var quarterback = positions.Single(p => p.Code == "QB");
            Assert.Null(quarterback.Rules);
            Assert.False(quarterback.IsPutAway);

            // The new positions joined the groups the team already had
            Assert.Equal("Receivers", groups.Single(g => g.Positions.Any(p => p.Code == "W")).Name);
            Assert.Equal("Defensive Backs", groups.Single(g => g.Positions.Any(p => p.Code == "S")).Name);
        }

        [Fact]
        public async Task PlayersListedAtAPositionThatIsPutAway_AreKept_AndComeBackWithTheGame()
        {
            var headCoach = await _world.AddTeamAsync();
            var brooks = await _world.AddPlayerAsync(headCoach, "Brooks", "Tight Ends");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);
            int tightEndId = await _world.PositionIdAsync(headCoach, "TE");

            using (var context = _world.NewContext())
            {
                await _world.DepthChart(context).AddPlayerAsync(headCoach, weekId, tightEndId, new AddPlacementDto { PlayerId = brooks.Id });
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);
            brooks = await _world.AsAsync(brooks.Id);

            using (var context = _world.NewContext())
            {
                var service = _world.DepthChart(context);

                // While the team plays the Canadian game the tight end cannot be reached: not looked at, not added to
                Assert.Empty((await service.GetMyPlacementsAsync(brooks, weekId))!);
                Assert.Null(await service.GetSlotAsync(headCoach, weekId, tightEndId));
                Assert.Null(await service.AddPlayerAsync(headCoach, weekId, tightEndId, new AddPlacementDto { PlayerId = brooks.Id }));

                int placementId = await context.DepthChartPlacements.Where(p => p.PositionId == tightEndId).Select(p => p.Id).SingleAsync();
                Assert.False(await service.RemovePlacementAsync(headCoach, placementId));
            }

            // Back in the American game, Brooks is the tight end again
            headCoach = await SwitchAsync(headCoach, FootballRules.American);
            brooks = await _world.AsAsync(brooks.Id);

            using (var context = _world.NewContext())
            {
                var mine = (await _world.DepthChart(context).GetMyPlacementsAsync(brooks, weekId))!;
                Assert.Equal("TE", mine.Single().Code);
                Assert.Equal(1, mine.Single().Rank);
            }
        }

        [Fact]
        public async Task ANewWeekCopiedFromAnEarlierOne_LeavesOutThePositionsThatArePutAway()
        {
            var headCoach = await _world.AddTeamAsync();
            var brooks = await _world.AddPlayerAsync(headCoach, "Brooks", "Tight Ends");
            var carter = await _world.AddPlayerAsync(headCoach, "Carter", "Quarterbacks");
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            using (var context = _world.NewContext())
            {
                var service = _world.DepthChart(context);
                await service.AddPlayerAsync(headCoach, weekId, await _world.PositionIdAsync(headCoach, "TE"), new AddPlacementDto { PlayerId = brooks.Id });
                await service.AddPlayerAsync(headCoach, weekId, await _world.PositionIdAsync(headCoach, "QB"), new AddPlacementDto { PlayerId = carter.Id });
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            using (var context = _world.NewContext())
            {
                var newWeek = await _world.DepthChart(context).CreateWeekAsync(headCoach, new CreateDepthChartWeekDto
                {
                    WeekStartDate = TestWorld.ThisMonday.AddDays(7),
                    CopyFromWeekId = weekId
                });

                // The quarterback is carried over; the tight end, who has no place in this game, is not
                var copied = await context.DepthChartPlacements
                    .Where(p => p.DepthChartWeekId == newWeek.Id)
                    .Select(p => p.Position!.Code)
                    .ToListAsync();
                Assert.Equal(new[] { "QB" }, copied);
            }
        }

        [Fact]
        public async Task APositionACoachAdds_BelongsToBothGames()
        {
            var headCoach = await _world.AddTeamAsync();
            int weekId = await _world.AddWeekAsync(headCoach, TestWorld.ThisMonday);

            using (var context = _world.NewContext())
            {
                var added = await _world.Groups(context).CreatePositionAsync(headCoach, new SavePositionDto
                {
                    PositionGroupId = await _world.GroupIdAsync(headCoach, "Defensive Backs"),
                    Code = "NB",
                    Name = "Nickel Back"
                });
                Assert.Null(added.Rules);
            }

            Assert.Contains("NB", await ChartCodesAsync(headCoach, weekId));

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);
            Assert.Contains("NB", await ChartCodesAsync(headCoach, weekId));
        }

        // ---------- The playbooks ----------

        [Fact]
        public async Task EachGameHasItsOwnPlaybooks_WithItsOwnBaseFormations()
        {
            var headCoach = await SwitchAsync(await _world.AddTeamAsync(), FootballRules.Canadian);

            using var context = _world.NewContext();
            var service = _world.Playbooks(context);
            var playbooks = await service.GetPlaybooksAsync(headCoach);

            // The same three playbooks, now for the Canadian game
            Assert.Equal(new[] { TeamUnit.Offence, TeamUnit.Defence, TeamUnit.SpecialTeams }, playbooks.Select(p => p.Unit));
            Assert.Equal(new[] { 5, 3, 5 }, playbooks.Select(p => p.FormationCount));
            Assert.All(playbooks, p =>
            {
                Assert.Equal(FootballRules.Canadian, p.Rules);
                Assert.Equal(12, p.PlayersPerSide);
                Assert.True(p.CanChange);
            });

            // Every base formation has twelve players, drawn for the Canadian field
            foreach (var playbook in playbooks)
            {
                var detail = await service.GetPlaybookAsync(headCoach, playbook.Id);
                Assert.All(detail!.Formations, f =>
                {
                    var diagram = DiagramValidator.Parse(f.DiagramJson);
                    Assert.Equal(12, diagram.Players.Count);
                    Assert.Equal("Canadian", diagram.Rules);
                });
            }

            // The American playbooks are still the team's
            Assert.Equal(6, await context.Playbooks.CountAsync(p => p.TeamId == headCoach.TeamId));
        }

        [Fact]
        public async Task ThePlaybooksOfTheOtherGame_ArePutAwayAsTheyWere_AndComeBackWithTheGame()
        {
            var headCoach = await _world.AddTeamAsync();
            int americanOffenceId = await _world.PlaybookIdAsync(headCoach, TeamUnit.Offence);

            // A play drawn while the team plays the American game
            using (var context = _world.NewContext())
            {
                await _world.Playbooks(context).CreatePlayAsync(headCoach, new SavePlayDto
                {
                    FormationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread"),
                    Name = "Slant",
                    DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X")
                });
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            using (var context = _world.NewContext())
            {
                var service = _world.Playbooks(context);

                // The list is the Canadian playbooks, which have no plays yet
                var playbooks = await service.GetPlaybooksAsync(headCoach);
                Assert.DoesNotContain(playbooks, p => p.Id == americanOffenceId);
                Assert.Equal(0, playbooks.Sum(p => p.PlayCount));

                // The American playbook can still be read, with its play, but nothing in it can be changed
                var putAway = await service.GetPlaybookAsync(headCoach, americanOffenceId);
                Assert.True(putAway!.IsPutAway);
                Assert.False(putAway.CanChange);
                Assert.Equal(1, putAway.PlayCount);

                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFormationAsync(headCoach, new SaveFormationDto
                {
                    PlaybookId = americanOffenceId,
                    Name = "Wishbone",
                    DiagramJson = putAway.Formations.First().DiagramJson
                }));
                Assert.Contains("put away", error.Message);
            }

            // Going back brings the American playbooks back as they were, and puts the Canadian ones away.
            // Nothing is made twice.
            headCoach = await SwitchAsync(headCoach, FootballRules.American);

            using (var context = _world.NewContext())
            {
                var playbooks = await _world.Playbooks(context).GetPlaybooksAsync(headCoach);
                Assert.Contains(playbooks, p => p.Id == americanOffenceId && p.PlayCount == 1);
                Assert.All(playbooks, p => Assert.Equal(FootballRules.American, p.Rules));
                Assert.Equal(6, await context.Playbooks.CountAsync(p => p.TeamId == headCoach.TeamId));
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            using (var context = _world.NewContext())
            {
                Assert.Equal(6, await context.Playbooks.CountAsync(p => p.TeamId == headCoach.TeamId));
                Assert.Equal(26, await context.Formations.CountAsync(f => f.TeamId == headCoach.TeamId));
                Assert.Equal(1, await context.Positions.CountAsync(p => p.TeamId == headCoach.TeamId && p.Code == "W"));
            }
        }

        [Fact]
        public async Task AFormation_HasAtMostTwelveASideInTheCanadianGame_AndElevenInTheAmerican()
        {
            var headCoach = await SwitchAsync(await _world.AddTeamAsync(), FootballRules.Canadian);
            int canadianOffenceId = await PlaybookIdAsync(headCoach, TeamUnit.Offence, FootballRules.Canadian);

            string twelve;
            using (var context = _world.NewContext())
            {
                var service = _world.Playbooks(context);
                twelve = (await service.GetPlaybookAsync(headCoach, canadianOffenceId))!.Formations.First().DiagramJson;

                // Twelve of our own is a full side
                var result = await service.CreateFormationAsync(headCoach, new SaveFormationDto
                {
                    PlaybookId = canadianOffenceId,
                    Name = "Twelve",
                    DiagramJson = twelve
                });
                Assert.True(result!.Published);

                // A thirteenth is one too many
                var thirteen = DiagramValidator.Parse(twelve);
                thirteen.Players.Add(new DiagramPlayer { Id = "extra", Label = "E", X = 10, Y = -6 });

                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFormationAsync(headCoach, new SaveFormationDto
                {
                    PlaybookId = canadianOffenceId,
                    Name = "Thirteen",
                    DiagramJson = DiagramValidator.Serialize(thirteen)
                }));
                Assert.Contains("12 players", error.Message);
            }

            // The same twelve do not fit an American playbook
            headCoach = await SwitchAsync(headCoach, FootballRules.American);

            int americanOffenceId = await PlaybookIdAsync(headCoach, TeamUnit.Offence, FootballRules.American);

            using (var context = _world.NewContext())
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _world.Playbooks(context).CreateFormationAsync(headCoach, new SaveFormationDto
                {
                    PlaybookId = americanOffenceId,
                    Name = "Twelve",
                    DiagramJson = twelve
                }));
                Assert.Contains("11 players", error.Message);
            }
        }

        [Fact]
        public async Task PlaybookChangesWaitingInTheOtherGame_ArePutAwayWithIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var receiversCoach = await _world.AddCoachAsync(headCoach, UserRole.PositionCoach, null, "Receivers");

            // A coach without the publish right sends a play for approval
            using (var context = _world.NewContext())
            {
                var result = await _world.Playbooks(context).CreatePlayAsync(receiversCoach, new SavePlayDto
                {
                    FormationId = await _world.FormationIdAsync(headCoach, "Shotgun Spread"),
                    Name = "Slant",
                    DiagramJson = await _world.DiagramAsync(headCoach, "Shotgun Spread", "X")
                });
                Assert.False(result!.Published);
            }

            using (var context = _world.NewContext())
            {
                Assert.Single(await _world.Approvals(context).GetRequestsAsync(headCoach, ApprovalStatus.Pending));
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            using (var context = _world.NewContext())
            {
                Assert.Empty(await _world.Approvals(context).GetRequestsAsync(headCoach, ApprovalStatus.Pending));
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.American);

            using (var context = _world.NewContext())
            {
                Assert.Single(await _world.Approvals(context).GetRequestsAsync(headCoach, ApprovalStatus.Pending));
            }
        }

        // ---------- Film: four downs in the American game, three in the Canadian ----------

        [Fact]
        public async Task APlay_IsTaggedWithADownTheGameHas()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);

            SaveClipDto Play(int startSeconds, int down) => new SaveClipDto
            {
                VideoId = film.VideoId,
                StartMs = startSeconds * 1000,
                EndMs = (startSeconds + 6) * 1000,
                Tag = new SavePlayTagDto { OffenceTeamId = film.OpponentId, PlayType = PlayType.Run, Down = down, Distance = 2 }
            };

            // American football: a fourth down is a down like any other
            using (var context = _world.NewContext())
            {
                var clip = await _world.Clips(context).CreateAsync(headCoach, Play(10, down: 4));
                Assert.Equal(4, clip!.Tag!.Down);
                Assert.Equal(4, (await _world.Videos(context).GetVideoAsync(headCoach, film.VideoId))!.Downs);
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            // Canadian football: there is no fourth down to tag a play with
            using (var context = _world.NewContext())
            {
                var clips = _world.Clips(context);

                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => clips.CreateAsync(headCoach, Play(20, down: 4)));
                Assert.Contains("three downs", error.Message);

                var third = await clips.CreateAsync(headCoach, Play(30, down: 3));
                Assert.Equal(3, third!.Tag!.Down);

                // The pages are told how many downs to offer
                Assert.Equal(3, (await _world.Videos(context).GetVideoAsync(headCoach, film.VideoId))!.Downs);
                Assert.All(await _world.Games(context).GetGamesAsync(headCoach, null), g => Assert.Equal(3, g.Downs));
            }
        }

        [Fact]
        public async Task TheTendencyCharts_HaveTheDownsOfTheGame_AndLeaveOutPlaysOnADownItDoesNotHave()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach, "Northgate Knights");
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 1, distance: 10, runs: 6, passes: 4);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 3, distance: 5, runs: 1, passes: 3);
            await _world.AddTaggedPlaysAsync(headCoach, film.VideoId, film.OpponentId, down: 4, distance: 1, runs: 2, passes: 0);

            var filter = new TendencyFilterDto { OffenceTeamId = film.OpponentId };

            // American football: four downs, and every play is counted
            using (var context = _world.NewContext())
            {
                var report = await _world.Tendencies(context).GetReportAsync(headCoach, filter);

                Assert.Equal(4, report.Downs);
                Assert.Equal(new[] { 1, 2, 3, 4 }, report.ByDown.Select(s => s.Down!.Value));
                Assert.Equal(4, report.Grid.Count);
                Assert.Equal(16, report.Selected.Plays);
                Assert.Equal(0, report.LeftOutPlays);

                var counted = await _world.Clips(context).GetClipsAsync(headCoach, new ClipFilterDto { Charted = true, OffenceTeamId = film.OpponentId });
                Assert.Equal(16, counted.Count);
            }

            headCoach = await SwitchAsync(headCoach, FootballRules.Canadian);

            // Canadian football: three downs. The two plays tagged as a fourth down are left out of every number,
            // and the report says how many they are.
            using (var context = _world.NewContext())
            {
                var report = await _world.Tendencies(context).GetReportAsync(headCoach, filter);

                Assert.Equal(3, report.Downs);
                Assert.Equal(new[] { 1, 2, 3 }, report.ByDown.Select(s => s.Down!.Value));
                Assert.Equal(3, report.Grid.Count);
                Assert.Equal(14, report.Selected.Plays);
                Assert.Equal(14, report.TaggedPlays);
                Assert.Equal(2, report.LeftOutPlays);
                Assert.Equal(report.Selected.Plays, report.ByDown.Sum(s => s.Plays));
            }

            // The list of plays behind a figure follows the same rule, and the plays left out can be listed by themselves
            using (var context = _world.NewContext())
            {
                var clips = _world.Clips(context);

                var counted = await clips.GetClipsAsync(headCoach, new ClipFilterDto { Charted = true, OffenceTeamId = film.OpponentId });
                Assert.Equal(14, counted.Count);
                Assert.DoesNotContain(counted, c => c.Tag!.Down == 4);

                var leftOut = await clips.GetClipsAsync(headCoach, new ClipFilterDto { Charted = false, OffenceTeamId = film.OpponentId });
                Assert.Equal(2, leftOut.Count);
                Assert.All(leftOut, c => Assert.Equal(4, c.Tag!.Down));

                // Every tagged play is still there to be found and tagged again
                var tagged = await clips.GetClipsAsync(headCoach, new ClipFilterDto { Tagged = true, OffenceTeamId = film.OpponentId });
                Assert.Equal(16, tagged.Count);
            }
        }

        [Fact]
        public async Task OneTeamsGame_DoesNotChangeAnotherTeams()
        {
            var tigersCoach = await _world.AddTeamAsync("Test Tigers");
            var bearsCoach = await _world.AddTeamAsync("Test Bears");
            int bearsWeekId = await _world.AddWeekAsync(bearsCoach, TestWorld.ThisMonday);

            await SwitchAsync(tigersCoach, FootballRules.Canadian);

            bearsCoach = await _world.AsAsync(bearsCoach.Id);
            Assert.Equal(FootballRules.American, bearsCoach.Rules);

            var codes = await ChartCodesAsync(bearsCoach, bearsWeekId);
            Assert.Contains("TE", codes);
            Assert.DoesNotContain("W", codes);

            using var context = _world.NewContext();
            Assert.Equal(3, await context.Playbooks.CountAsync(p => p.TeamId == bearsCoach.TeamId));
        }
    }
}
