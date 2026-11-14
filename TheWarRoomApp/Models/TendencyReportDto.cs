namespace TheWarRoomApp.Models
{
    // Run and pass tendencies for a set of games and a team on offence.
    // They describe what was recorded on film; they do not predict the next play.
    public class TendencyReportDto
    {
        // The tendency for exactly what was asked: the chosen games, team, down and distance
        public TendencySplitDto Selected { get; set; } = new TendencySplitDto();

        // The same plays split by down. The distance filter still applies, so this shows every down at that distance.
        public List<TendencySplitDto> ByDown { get; set; } = new List<TendencySplitDto>();

        // The same plays split by distance. The down filter still applies, so this shows every distance on that down.
        public List<TendencySplitDto> ByDistance { get; set; } = new List<TendencySplitDto>();

        // Every down against every distance, for the chosen games and team
        public List<TendencyGridRowDto> Grid { get; set; } = new List<TendencyGridRowDto>();

        // How many tagged plays the chosen games and team have in all, and how many games they come from
        public int TaggedPlays { get; set; }
        public int Games { get; set; }
    }

    // The runs and passes in one group of tagged plays, and the tendency worked out from them
    public class TendencySplitDto
    {
        // What the group is: one down, one distance range, both (a cell of the grid), or neither
        public int? Down { get; set; }
        public DistanceRange? Distance { get; set; }

        public int Runs { get; set; }
        public int Passes { get; set; }

        // The sample size: runs + passes
        public int Plays { get; set; }

        // Percentages to one decimal. Both are empty when there are no plays, which is shown as "No data".
        public decimal? RunPercent { get; set; }
        public decimal? PassPercent { get; set; }
    }

    // One down of the grid, with a cell for each distance range from short to very long
    public class TendencyGridRowDto
    {
        public int Down { get; set; }
        public List<TendencySplitDto> Cells { get; set; } = new List<TendencySplitDto>();
    }
}
