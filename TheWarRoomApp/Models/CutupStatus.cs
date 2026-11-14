using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Where a cutup stands. Its plays can be changed only while it is a Draft;
    // after that it is being made into one video, or already has been.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CutupStatus
    {
        Draft = 1,
        Queued = 2,
        Rendering = 3,
        Ready = 4,
        Failed = 5
    }
}
