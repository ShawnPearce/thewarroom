using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.DTOs
{
    // Moves a play one step earlier (Up) or later (Down) in its cutup
    public class MoveCutupPlayDto
    {
        public MoveDirection Direction { get; set; }
    }
}
