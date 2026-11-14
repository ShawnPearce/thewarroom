using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Where a player stands on the roster.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RosterStatus
    {
        Active = 1,
        Injured = 2,
        Inactive = 3
    }
}
