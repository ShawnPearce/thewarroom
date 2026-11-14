namespace TheWarRoomApp.Models
{
    // How often one player was present, late, absent or excused over a period
    public class AttendanceSummaryDto
    {
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int Excused { get; set; }
    }
}
