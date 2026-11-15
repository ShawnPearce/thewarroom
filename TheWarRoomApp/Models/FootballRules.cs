using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Copy of the API's FootballRules: the game a team plays. The two must list the same names.
    // American football has 11 players a side on the field; Canadian football has 12, on a wider field.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FootballRules
    {
        American = 1,
        Canadian = 2
    }
}
