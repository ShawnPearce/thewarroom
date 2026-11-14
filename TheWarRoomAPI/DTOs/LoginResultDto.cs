using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // What the MVC app needs to sign a user in after the API has checked the password
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

        // Identifies this sign-in; the MVC app sends it back on every later call
        public string SessionToken { get; set; } = string.Empty;
    }
}
