using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // What a playbook change request is about.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlaybookTargetType
    {
        Formation = 1,
        Play = 2
    }
}
