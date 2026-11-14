using System.ComponentModel.DataAnnotations;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // The football details of a player. Name and email are changed through the User endpoint.
    public class UpdateRosterEntryDto
    {
        [Range(0, 99, ErrorMessage = "Jersey number must be between 0 and 99.")]
        public int? JerseyNumber { get; set; }

        [Range(9, 12, ErrorMessage = "Grade must be between 9 and 12.")]
        public int? Grade { get; set; }

        [Range(48, 90, ErrorMessage = "Height must be between 48 and 90 inches.")]
        public int? HeightInches { get; set; }

        [Range(80, 400, ErrorMessage = "Weight must be between 80 and 400 pounds.")]
        public int? WeightLbs { get; set; }

        public RosterStatus Status { get; set; }
    }
}
