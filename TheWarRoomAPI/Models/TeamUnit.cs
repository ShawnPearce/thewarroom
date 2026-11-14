using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // The three sides of a football team. Coordinators, position groups and playbooks each belong to one.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamUnit
    {
        Offence = 1,
        Defence = 2,
        SpecialTeams = 3
    }
}
