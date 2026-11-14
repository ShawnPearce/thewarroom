using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // What the offence did on a tagged play. The tendency charts count these.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlayType
    {
        Run = 1,
        Pass = 2
    }
}
