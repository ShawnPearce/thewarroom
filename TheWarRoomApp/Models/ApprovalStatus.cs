using System.Text.Json.Serialization;

namespace TheWarRoomApp.Models
{
    // Where a playbook change request stands.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Withdrawn = 4
    }
}
