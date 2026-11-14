namespace TheWarRoomAPI.Services
{
    // Thrown by a service when the current user is not allowed to do what was asked.
    // Controllers catch it and answer 403 Forbidden.
    // (A broken business rule throws InvalidOperationException instead, which becomes 409 Conflict.)
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message)
        {
        }
    }
}
