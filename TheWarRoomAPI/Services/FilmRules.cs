namespace TheWarRoomAPI.Services
{
    // The one permission check shared by the film services.
    // Game film (games, videos, clips, tags) is the coaches' working material.
    // Players never open it: they watch the cutups that are shared with them (see CutupService).
    public static class FilmRules
    {
        public static void EnsureCoach(CurrentUser currentUser)
        {
            if (!AccessRules.CanManageFilm(currentUser))
            {
                throw new ForbiddenException("Game film is for coaches. Players watch the cutups that are shared with them.");
            }
        }
    }
}
