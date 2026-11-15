namespace TheWarRoomApp.Models
{
    // The playbook changes page: what is waiting for the head coach, what the head coach sent back to this coach,
    // and what was decided earlier
    public class ApprovalsViewModel
    {
        // "waiting", "sent-back" or "history"
        public string Show { get; set; } = "waiting";

        public List<ApprovalRequestDto> Waiting { get; set; } = new List<ApprovalRequestDto>();

        // This coach's own changes that the head coach rejected, as long as they are news or can still be sent again
        public List<ApprovalRequestDto> SentBack { get; set; } = new List<ApprovalRequestDto>();
        public List<ApprovalRequestDto> History { get; set; } = new List<ApprovalRequestDto>();
    }
}
