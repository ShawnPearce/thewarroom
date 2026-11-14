namespace TheWarRoomAPI.Models
{
    // What a coach recorded about the play in a clip: run or pass, the down, the distance to go,
    // and which team had the ball. A clip without a tag is not counted in the tendency charts.
    public class PlayTag
    {
        public int Id { get; set; }

        // Foreign key for the Team entity
        public int TeamId { get; set; }
        public Team? Team { get; set; }

        // Foreign key for the Clip entity (one tag per clip)
        public int ClipId { get; set; }
        public Clip? Clip { get; set; }

        public PlayType PlayType { get; set; }

        // 1 to 4
        public int Down { get; set; }

        // Yards to go for a first down
        public int Distance { get; set; }

        // Foreign key for the ScoutedTeam entity: the team on offence, one of the two teams in the game
        public int OffenceTeamId { get; set; }
        public ScoutedTeam? OffenceTeam { get; set; }
    }
}
