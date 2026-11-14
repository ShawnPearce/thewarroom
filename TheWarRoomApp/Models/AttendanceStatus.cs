using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // What a coach records for a player at an event.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AttendanceStatus
    {
        Present = 1,
        Late = 2,
        Absent = 3,
        Excused = 4
    }
}
