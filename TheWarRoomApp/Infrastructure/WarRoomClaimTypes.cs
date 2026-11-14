namespace TheWarRoomApp.Infrastructure
{
    // Names of the extra values kept in the login cookie, next to the standard name, email and role claims
    public static class WarRoomClaimTypes
    {
        public const string TeamId = "warroom:team_id";
        public const string TeamName = "warroom:team_name";
        public const string CoordinatorUnit = "warroom:coordinator_unit";

        // Identifies this sign-in to the API (the LoginSession token)
        public const string SessionToken = "warroom:session_token";

        // "true" while the user still has the temporary password they were given
        public const string MustChangePassword = "warroom:must_change_password";
    }
}
