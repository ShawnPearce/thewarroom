namespace TheWarRoomApp.Models
{
    // The drawings on a play: the moments the video stops at, with the arrows and text shown there.
    // Sent as the JSON text the editor works with (the API's Diagrams/FilmDrawings.cs describes it). Empty takes the drawings away.
    public class SaveAnnotationsDto
    {
        public string? AnnotationsJson { get; set; }
    }
}
