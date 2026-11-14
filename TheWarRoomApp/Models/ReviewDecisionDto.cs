using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The head coach's optional note when approving or rejecting
    public class ReviewDecisionDto
    {
        [StringLength(500, ErrorMessage = "The note can't exceed 500 characters.")]
        [Display(Name = "Note for the coach")]
        public string? Note { get; set; }
    }
}
