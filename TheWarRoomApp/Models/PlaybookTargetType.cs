using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // What a playbook change request is about.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlaybookTargetType
    {
        Formation = 1,
        Play = 2
    }
}
