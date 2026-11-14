using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // What the offence did on a tagged play. The tendency charts count these.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlayType
    {
        Run = 1,
        Pass = 2
    }
}
