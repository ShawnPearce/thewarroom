using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // What a player was studying. Film time and playbook time are kept and reported separately.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ViewingKind
    {
        Film = 1,
        Playbook = 2
    }
}
