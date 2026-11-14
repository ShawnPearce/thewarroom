namespace TheWarRoomApp.Models
{
    // What the API returns after it has checked the password (or registered a team)
    public class LoginResultDto
    {
        public int UserId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public TeamUnit? CoordinatorUnit { get; set; }
        public bool MustChangePassword { get; set; }

        // Identifies this sign-in; it is kept in the login cookie and sent back on every call
        public string SessionToken { get; set; } = string.Empty;
    }
}
