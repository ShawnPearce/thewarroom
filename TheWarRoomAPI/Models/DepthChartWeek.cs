using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TheWarRoomAPI.Models
{
    // The depth chart for one week. A team has at most one per week.
    [Index(nameof(TeamId), nameof(WeekStartDate), IsUnique = true)]
    public class DepthChartWeek
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Always the Monday of the week
        public DateOnly WeekStartDate { get; set; }

        // Optional name, for example "Week 3 vs Central"
        [StringLength(60)]
        public string? Label { get; set; }

        public DateTime CreatedAt { get; set; }

        //Navigation property to represent the One-to-Many relationship with DepthChartPlacement
        public List<DepthChartPlacement>? Placements { get; set; }
    }
}
