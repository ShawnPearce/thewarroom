using System.Text.Json.Serialization;

namespace TheWarRoomAPI.Models
{
    // Where a playbook change request stands.
    // The numbers are stored in the database, so they must never be reordered.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Withdrawn = 4
    }
}
