using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheWarRoomApp.Models
{
    // The add/edit game form, with the teams its two drop-downs choose from
    public class GameFormViewModel
    {
        public int Id { get; set; }
        public SaveGameDto Game { get; set; } = new SaveGameDto();

        // Filled in by the controller for the page, never read from the form
        [BindNever]
        [ValidateNever]
        public List<ScoutedTeamDto> Teams { get; set; } = new List<ScoutedTeamDto>();
    }
}
