using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // What the film services share: the one permission check, and how many downs the team's game has.
    // Game film (games, videos, clips, tags) is the coaches' working material.
    // Players never open it: they watch the cutups that are shared with them (see CutupService).
    public static class FilmRules
    {
        // How many downs the offence has to gain ten yards: four in American football, three in Canadian football.
        // A play is tagged with one of them, and the tendency charts have a row for each.
        public static int Downs(FootballRules rules)
        {
            return rules == FootballRules.Canadian ? 3 : 4;
        }

        public static void EnsureCoach(CurrentUser currentUser)
        {
            if (!AccessRules.CanManageFilm(currentUser))
            {
                throw new ForbiddenException("Game film is for coaches. Players watch the cutups that are shared with them.");
            }
        }
    }
}
