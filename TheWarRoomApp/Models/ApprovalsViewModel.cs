namespace TheWarRoomApp.Models
{
    // The playbook changes page: what is waiting for the head coach, and what was decided earlier
    public class ApprovalsViewModel
    {
        // "waiting" or "history"
        public string Show { get; set; } = "waiting";

        public List<ApprovalRequestDto> Waiting { get; set; } = new List<ApprovalRequestDto>();
        public List<ApprovalRequestDto> History { get; set; } = new List<ApprovalRequestDto>();
    }
}
