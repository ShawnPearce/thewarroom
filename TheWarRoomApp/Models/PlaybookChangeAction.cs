using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // What a playbook change request asks for.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlaybookChangeAction
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }
}
