namespace TheWarRoomAPI.DTOs
{
    // A team that appears on film: the team itself, or an opponent
    public class ScoutedTeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // True for the team's own entry. It is renamed in the team settings and cannot be removed.
        public bool IsOwnTeam { get; set; }

        // How many games on film this team played in
        public int GameCount { get; set; }
    }
}
