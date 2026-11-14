namespace TheWarRoomAPI.Filters
{
    // Names of the headers the MVC app adds to every call it makes to this API
    public static class ApiHeaders
    {
        // Id of the signed-in user the call is made for
        public const string UserId = "X-User-Id";

        // Identifies the sign-in (LoginSession) the call belongs to
        public const string SessionToken = "X-Session-Token";
    }
}
