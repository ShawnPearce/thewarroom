using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheWarRoomApp.Models
{
    // The add/edit play form. Times are typed the way people say them ("12:34");
    // the controller turns them into the milliseconds the API keeps.
    public class ClipFormViewModel
    {
        public int Id { get; set; }
        public int VideoId { get; set; }

        [Required(ErrorMessage = "Enter where the play starts.")]
        [Display(Name = "Starts at")]
        public string Start { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter where the play ends.")]
        [Display(Name = "Ends at")]
        public string End { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "The note can't exceed 100 characters.")]
        [Display(Name = "Note")]
        public string? Title { get; set; }

        // The tag. Leaving the play type empty means the play is not tagged.
        [Display(Name = "Play")]
        public PlayType? PlayType { get; set; }

        [Display(Name = "Team on offence")]
        public int? OffenceTeamId { get; set; }

        [Range(1, 4, ErrorMessage = "The down must be between 1 and 4.")]
        public int? Down { get; set; }

        [Range(1, 99, ErrorMessage = "The distance must be between 1 and 99 yards.")]
        [Display(Name = "Yards to go")]
        public int? Distance { get; set; }

        // The video the play is cut from. Filled in by the controller for the page, never read from the form.
        [BindNever]
        [ValidateNever]
        public VideoDetailDto Video { get; set; } = new VideoDetailDto();
    }
}
