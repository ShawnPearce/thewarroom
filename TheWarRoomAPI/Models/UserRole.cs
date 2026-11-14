using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // The four kinds of account a team can have.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserRole
    {
        HeadCoach = 1,
        Coordinator = 2,
        PositionCoach = 3,
        Player = 4
    }
}
