using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // The game a team plays. American football has 11 players a side on the field; Canadian football has 12,
    // on a wider field. The head coach chooses it in the team's settings.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FootballRules
    {
        American = 1,
        Canadian = 2
    }
}
