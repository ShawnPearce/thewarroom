using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // The distance to go for a first down, grouped the way coaches talk about it.
    // The yards behind each name are in TendencyCalculator.Bounds.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DistanceRange
    {
        Short = 1,      // 1 to 3 yards
        Medium = 2,     // 4 to 6
        Long = 3,       // 7 to 10
        VeryLong = 4    // 11 or more
    }
}
