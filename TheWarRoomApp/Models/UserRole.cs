using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Copy of the API's UserRole. The two must list the same names.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserRole
    {
        HeadCoach = 1,
        Coordinator = 2,
        PositionCoach = 3,
        Player = 4
    }
}
