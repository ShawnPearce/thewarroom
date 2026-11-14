using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // Who a cutup is shared with.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FilmAudience
    {
        Team = 1,
        Group = 2,
        Player = 3
    }
}
