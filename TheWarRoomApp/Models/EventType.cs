using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // The kinds of event on a team's schedule.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventType
    {
        Practice = 1,
        Game = 2,
        Meeting = 3
    }
}
