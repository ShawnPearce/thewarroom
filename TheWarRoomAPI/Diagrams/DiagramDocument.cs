namespace TheWarRoomAPI.Diagrams
{
    // The drawing of a formation or a play, as it is stored (JSON) and as the browser draws it.
    //
    // Everything is measured in yards:
    //   x  runs across the field from the centre:  -26.67 (left sideline) to +26.67 (right sideline)
    //   y  runs up the field from the line of scrimmage: negative is the offence's backfield, positive is downfield
    //
    // A formation is the same document with no paths and no text.
    public class DiagramDocument
    {
        public int SchemaVersion { get; set; } = 1;

        // "Offence", "Defence" or "SpecialTeams"
        public string Unit { get; set; } = "Offence";

        // How much of the field is shown
        public DiagramView View { get; set; } = new DiagramView();

        // Where the ball sits on the line of scrimmage
        public double BallX { get; set; }

        public List<DiagramPlayer> Players { get; set; } = new List<DiagramPlayer>();
        public List<DiagramDrawing> Drawings { get; set; } = new List<DiagramDrawing>();
    }

    public class DiagramView
    {
        public double YMin { get; set; } = -12;
        public double YMax { get; set; } = 18;
    }

    public class DiagramPlayer
    {
        // Stays the same from the formation to its plays, so two versions can be compared player by player
        public string Id { get; set; } = string.Empty;

        // "own" for the team's players, "opp" for opponents drawn for reference
        public string Team { get; set; } = "own";

        public string Label { get; set; } = string.Empty;

        // "circle", "square" or "triangle"
        public string Shape { get; set; } = "circle";

        public double X { get; set; }
        public double Y { get; set; }

        // The player's routes, blocks and motion
        public List<DiagramPath> Paths { get; set; } = new List<DiagramPath>();
    }

    public class DiagramPath
    {
        // "route", "block" or "motion"
        public string Kind { get; set; } = "route";

        // Waypoints as [x, y] offsets from the player, so moving the player carries the path along
        public List<double[]> Pts { get; set; } = new List<double[]>();
    }

    public class DiagramDrawing
    {
        public string Id { get; set; } = string.Empty;

        // Only "text" for now
        public string Type { get; set; } = "text";

        public double X { get; set; }
        public double Y { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
