using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The attendance summary page: counts per player, optionally limited to a period
    public class AttendanceSummaryViewModel
    {
        public List<AttendanceSummaryDto> Rows { get; set; } = new List<AttendanceSummaryDto>();

        [DataType(DataType.Date)]
        public DateOnly? From { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? To { get; set; }
    }
}
