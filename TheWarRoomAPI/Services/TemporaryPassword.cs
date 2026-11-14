using System.Security.Cryptography;

namespace TheWarRoomAPI.Services
{
    // Creates the temporary password a new account (or a reset account) starts with.
    // The head coach is shown it once and passes it on; the user must replace it at first sign-in.
    public static class TemporaryPassword
    {
        // Letters and digits that are hard to mix up when read aloud or written down (no 0/O, 1/l/I)
        private const string Characters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static string Create()
        {
            return RandomNumberGenerator.GetString(Characters, 12);
        }
    }
}
