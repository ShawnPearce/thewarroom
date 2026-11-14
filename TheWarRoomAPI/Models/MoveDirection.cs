using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // Which way something is moved in an ordered list: Up is towards the top.
    // On a depth chart that is towards the starter's spot; in a cutup, towards the first play.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MoveDirection
    {
        Up = 1,
        Down = 2
    }
}
