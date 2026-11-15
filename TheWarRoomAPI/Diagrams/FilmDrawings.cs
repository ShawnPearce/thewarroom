namespace TheWarRoomAPI.Diagrams
{
    // What a coach drew on a play. It is stored as JSON text with the clip.
    //
    // A drawing belongs to one moment of the play. When the play is shown, the video stops at that moment
    // for a few seconds with the arrows and text over the picture, and then carries on.
    public class FilmDrawings
    {
        public int SchemaVersion { get; set; } = 1;

        // The moments the video stops at, in the order they come
        public List<FilmStop> Stops { get; set; } = new List<FilmStop>();
    }

    public class FilmStop
    {
        // Where the video stops: milliseconds from the start of the video the play is cut from
        public int AtMs { get; set; }

        // How long it stays stopped, in milliseconds
        public int HoldMs { get; set; }

        public List<FilmMark> Marks { get; set; } = new List<FilmMark>();
    }

    // One arrow or one piece of text. Positions are parts of the picture:
    // 0 is its left or top edge and 1 its right or bottom edge, whatever size the video is shown at.
    public class FilmMark
    {
        // "arrow" or "text"
        public string Type { get; set; } = "arrow";

        // An arrow runs from (X1, Y1) to its head at (X2, Y2). A text is centred on (X1, Y1).
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // For a text
        public string? Text { get; set; }

        // "yellow", "red" or "white"
        public string Colour { get; set; } = "yellow";
    }
}
