using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // Where a cutup stands. Its plays can be changed only while it is a Draft;
    // after that it is being made into one video, or already has been.
    // The numbers are stored in the database, so they must never be reordered.
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
