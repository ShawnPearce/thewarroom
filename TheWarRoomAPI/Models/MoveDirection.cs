using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // Which way a player is moved on a depth chart: Up is towards the starter's spot
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MoveDirection
    {
        Up = 1,
        Down = 2
    }
}
