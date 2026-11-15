namespace TheWarRoomApp.Infrastructure
{
    // The "Site" section of appsettings.json: switches that depend on where the site runs
    public class SiteOptions
    {
        public const string Section = "Site";

        // Whether the pages offer "Register your team". The API has the same switch and is the one that enforces it.
        public bool AllowRegistration { get; set; } = true;

        // A folder for the keys that protect sign-in cookies. On a server it is set, so that people
        // stay signed in when the site is restarted or a new version is put in place.
        public string? KeysFolder { get; set; }
    }
}
