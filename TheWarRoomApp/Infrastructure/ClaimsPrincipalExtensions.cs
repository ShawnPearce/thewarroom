using System.Security.Claims;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Infrastructure
{
    // Small helpers for reading the signed-in user's details from the login cookie in views.
    // These decide what the menus show; what a user may actually do is decided by the API.
    public static class ClaimsPrincipalExtensions
    {
        public static string GetTeamName(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(WarRoomClaimTypes.TeamName) ?? string.Empty;
        }

        // The id of the signed-in user, to tell their own requests from other coaches' (0 when nobody is signed in)
        public static int GetUserId(this ClaimsPrincipal user)
        {
            return int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;
        }

        public static bool IsHeadCoach(this ClaimsPrincipal user)
        {
            return user.IsInRole(nameof(UserRole.HeadCoach));
        }

        public static bool IsCoach(this ClaimsPrincipal user)
        {
            return user.Identity?.IsAuthenticated == true && !user.IsInRole(nameof(UserRole.Player));
        }

        public static bool IsPlayer(this ClaimsPrincipal user)
        {
            return user.IsInRole(nameof(UserRole.Player));
        }

        // "Head coach", "Offence coordinator", "Position coach" or "Player"
        public static string GetRoleLabel(this ClaimsPrincipal user)
        {
            if (user.IsInRole(nameof(UserRole.HeadCoach)))
            {
                return "Head coach";
            }
            if (user.IsInRole(nameof(UserRole.Coordinator)))
            {
                var unit = user.FindFirstValue(WarRoomClaimTypes.CoordinatorUnit);
                return unit switch
                {
                    nameof(TeamUnit.Offence) => "Offence coordinator",
                    nameof(TeamUnit.Defence) => "Defence coordinator",
                    nameof(TeamUnit.SpecialTeams) => "Special teams coordinator",
                    _ => "Coordinator"
                };
            }
            if (user.IsInRole(nameof(UserRole.PositionCoach)))
            {
                return "Position coach";
            }
            return "Player";
        }
    }
}
