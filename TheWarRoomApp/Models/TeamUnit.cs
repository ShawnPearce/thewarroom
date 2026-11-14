using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Copy of the API's TeamUnit. The two must list the same names.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TeamUnit
    {
        Offence = 1,
        Defence = 2,
        SpecialTeams = 3
    }
}
