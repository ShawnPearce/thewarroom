using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // What a playbook change request asks for.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlaybookChangeAction
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }
}
