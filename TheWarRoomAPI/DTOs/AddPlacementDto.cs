using System.ComponentModel.DataAnnotations;

namespace TheWarRoomAPI.DTOs
{
    public class AddPlacementDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a player.")]
        public int PlayerId { get; set; }
    }
}
