using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class CreateDepthChartWeekDto
    {
        // Any day of the week; the server stores the Monday of that week
        public DateOnly WeekStartDate { get; set; }

        [StringLength(60, ErrorMessage = "Label can't exceed 60 characters.")]
        public string? Label { get; set; }

        // When set, the new week starts as a copy of this week's depth chart
        public int? CopyFromWeekId { get; set; }
    }
}
