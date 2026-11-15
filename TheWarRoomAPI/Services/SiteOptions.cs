namespace TheWarRoomAPI.Services
{
    // The "Site" section of appsettings.json: switches that depend on where the application runs
    public class SiteOptions
    {
        public const string Section = "Site";

        // Whether anyone may register a new team.
        // Switched off on a public demo site, where it would let any visitor open a workspace and upload video.
        public bool AllowRegistration { get; set; } = true;
    }
}
