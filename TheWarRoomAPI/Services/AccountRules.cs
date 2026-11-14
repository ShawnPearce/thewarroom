using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Data;

namespace TheWarRoomAPI.Services
{
    // Rules about accounts that several services share
    public static class AccountRules
    {
        // Business rule: an email address can belong to only one account.
        // The Email column ignores upper/lower case, so "A@x.com" and "a@x.com" are the same address.
        public static async Task EnsureEmailIsUniqueAsync(AppDbContext context, string email, int exceptUserId = 0)
        {
            bool exists = await context.Users.AnyAsync(u => u.Email == email && u.Id != exceptUserId);
            if (exists)
            {
                throw new InvalidOperationException("An account with this email address already exists.");
            }
        }

        // Ends every open sign-in of a user, so their login cookie stops working.
        // Used when an account is deactivated or its password is reset.
        public static async Task EndOpenSessionsAsync(AppDbContext context, int userId)
        {
            var openSessions = await context.LoginSessions
                .Where(s => s.UserId == userId && s.EndedAt == null)
                .ToListAsync();

            foreach (var session in openSessions)
            {
                // The session counts up to the last request that was seen, not up to now
                session.EndedAt = session.LastSeenAt;
            }
        }
    }
}
