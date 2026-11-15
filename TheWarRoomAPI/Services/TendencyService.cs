using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    public class TendencyService : ITendencyService
    {
        private readonly AppDbContext _context;

        public TendencyService(AppDbContext context)
        {
            _context = context;
        }

        // What the calculation needs to know about one tagged play
        private record TaggedPlay(int GameId, int Down, int Distance, PlayType PlayType);

        public async Task<TendencyReportDto> GetReportAsync(CurrentUser currentUser, TendencyFilterDto filter)
        {
            FilmRules.EnsureCoach(currentUser);

            // The tagged plays of this team's film, for the chosen games and the chosen team on offence.
            // A clip without a tag has no row here, so it is never counted.
            var query = _context.PlayTags.Where(t => t.TeamId == currentUser.TeamId);

            if (filter.GameIds.Count > 0)
            {
                query = query.Where(t => filter.GameIds.Contains(t.Clip!.Video!.GameId));
            }
            if (filter.OffenceTeamId != null)
            {
                query = query.Where(t => t.OffenceTeamId == filter.OffenceTeamId);
            }

            var plays = await query
                .Select(t => new TaggedPlay(t.Clip!.Video!.GameId, t.Down, t.Distance, t.PlayType))
                .ToListAsync();

            // Business rule: the charts have the downs of the game the team plays, four in American football
            // and three in Canadian football. A play tagged with a down that game does not have (a fourth down
            // on a team that now plays the Canadian game) is left out of every number, and the report says how many.
            int downs = FilmRules.Downs(currentUser.Rules);
            int leftOut = plays.Count(p => p.Down > downs);
            plays = plays.Where(p => p.Down <= downs).ToList();

            bool IsChosenDown(TaggedPlay play) => filter.Down == null || play.Down == filter.Down;
            bool IsChosenDistance(TaggedPlay play) => filter.Distance == null || TendencyCalculator.RangeOf(play.Distance) == filter.Distance;

            var report = new TendencyReportDto
            {
                Downs = downs,
                LeftOutPlays = leftOut,
                TaggedPlays = plays.Count,
                Games = plays.Select(p => p.GameId).Distinct().Count(),

                // Exactly what was asked for
                Selected = Count(plays.Where(p => IsChosenDown(p) && IsChosenDistance(p)), filter.Down, filter.Distance)
            };

            // Each down, at the chosen distance
            for (int down = 1; down <= downs; down++)
            {
                int thisDown = down;
                report.ByDown.Add(Count(plays.Where(p => p.Down == thisDown && IsChosenDistance(p)), thisDown, filter.Distance));
            }

            // Each distance range, on the chosen down
            foreach (var range in TendencyCalculator.Ranges)
            {
                report.ByDistance.Add(Count(
                    plays.Where(p => TendencyCalculator.RangeOf(p.Distance) == range && IsChosenDown(p)), filter.Down, range));
            }

            // Every down against every distance range
            for (int down = 1; down <= downs; down++)
            {
                int thisDown = down;
                var row = new TendencyGridRowDto { Down = thisDown };

                foreach (var range in TendencyCalculator.Ranges)
                {
                    row.Cells.Add(Count(
                        plays.Where(p => p.Down == thisDown && TendencyCalculator.RangeOf(p.Distance) == range), thisDown, range));
                }

                report.Grid.Add(row);
            }

            return report;
        }

        // Counts the runs and the passes of a group of plays and hands them to the calculator
        private static TendencySplitDto Count(IEnumerable<TaggedPlay> plays, int? down, DistanceRange? distance)
        {
            var group = plays.ToList();

            int runs = group.Count(p => p.PlayType == PlayType.Run);
            int passes = group.Count(p => p.PlayType == PlayType.Pass);

            return TendencyCalculator.Split(runs, passes, down, distance);
        }
    }
}
