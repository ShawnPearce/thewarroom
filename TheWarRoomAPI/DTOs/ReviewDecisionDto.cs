using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    // The head coach's optional note when approving or rejecting
    public class ReviewDecisionDto
    {
        [StringLength(500, ErrorMessage = "The note can't exceed 500 characters.")]
        public string? Note { get; set; }
    }
}
