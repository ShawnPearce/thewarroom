namespace TheWarRoomApp.Models
{
    // The body sent to POST api/Auth/change-password
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
