using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Where an uploaded video stands. Only a video that is Ready can be cut into plays.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum VideoStatus
    {
        Uploading = 1,
        Processing = 2,
        Ready = 3,
        Failed = 4
    }
}
