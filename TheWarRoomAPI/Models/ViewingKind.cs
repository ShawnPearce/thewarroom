using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // What a player was studying. Film time and playbook time are kept and reported separately.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ViewingKind
    {
        Film = 1,
        Playbook = 2
    }
}
