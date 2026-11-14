namespace TheWarRoomAPI.DTOs
{
    // What happened to a playbook change: it was published at once, or it is waiting for approval
    public class PlaybookChangeResultDto
    {
        // True when the change is already live; false when it was queued for the head coach
        public bool Published { get; set; }

        public int RequestId { get; set; }

        // The formation or play the change ended up on (empty when a new one is still waiting, or after a delete)
        public int? FormationId { get; set; }
        public int? PlayId { get; set; }
    }
}
