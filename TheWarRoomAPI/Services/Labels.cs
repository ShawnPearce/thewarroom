using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Wording used in messages and titles
    public static class Labels
    {
        public static string Unit(TeamUnit unit)
        {
            return unit switch
            {
                TeamUnit.Offence => "Offence",
                TeamUnit.Defence => "Defence",
                TeamUnit.SpecialTeams => "Special teams",
                _ => unit.ToString()
            };
        }

        public static string FullName(User user)
        {
            return $"{user.FirstName} {user.LastName}";
        }
    }
}
