namespace TheWarRoomAPI.DTOs
{
    // Returned once, when an account is created or its password is reset.
    // Only the hash is stored, so the temporary password cannot be shown again later.
    public class TemporaryPasswordDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
