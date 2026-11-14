using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Data
{
    // What every new team starts with: a standard set of position groups and depth-chart positions,
    // a playbook for each unit holding the base formations, and the team's own entry in the film library.
    // Used when a team registers and when the demo teams are created, so both get the same setup.
    // Coaches can change all of it afterwards.
    public static class TeamDefaults
    {
        public static void AddTo(Team team, AppDbContext context, DateTime now)
        {
            AddGroupsAndPositions(team, context);
            AddPlaybooks(team, context, now);

            // Games on film are played between two "teams on film"; this one stands for the team itself
            context.ScoutedTeams.Add(new ScoutedTeam { Team = team, Name = team.Name, IsOwnTeam = true });
        }

        // One playbook per unit, each starting with a copy of the base formations for that unit
        private static void AddPlaybooks(Team team, AppDbContext context, DateTime now)
        {
            var templates = FormationTemplates.All();

            AddPlaybook(TeamUnit.Offence, "Offence playbook");
            AddPlaybook(TeamUnit.Defence, "Defence playbook");
            AddPlaybook(TeamUnit.SpecialTeams, "Special teams playbook");

            void AddPlaybook(TeamUnit unit, string name)
            {
                var playbook = new Playbook
                {
                    Team = team,
                    Unit = unit,
                    Name = name,
                    Formations = new List<Formation>()
                };

                foreach (var template in templates.Where(t => t.Unit == unit))
                {
                    playbook.Formations.Add(new Formation
                    {
                        Team = team,
                        Name = template.Name,
                        DiagramJson = DiagramValidator.Serialize(template.Diagram),
                        Version = 1,
                        UpdatedAt = now
                    });
                }

                context.Playbooks.Add(playbook);
            }
        }

        private static void AddGroupsAndPositions(Team team, AppDbContext context)
        {
            int groupOrder = 0;
            int positionOrder = 0;

            // Offence
            AddGroup(TeamUnit.Offence, "Quarterbacks", ("QB", "Quarterback"));
            AddGroup(TeamUnit.Offence, "Running Backs", ("RB", "Running Back"), ("FB", "Fullback"));
            AddGroup(TeamUnit.Offence, "Receivers", ("X", "X Receiver"), ("Y", "Y Receiver"), ("Z", "Z Receiver"));
            AddGroup(TeamUnit.Offence, "Tight Ends", ("TE", "Tight End"));
            AddGroup(TeamUnit.Offence, "Offensive Line",
                ("LT", "Left Tackle"), ("LG", "Left Guard"), ("C", "Centre"), ("RG", "Right Guard"), ("RT", "Right Tackle"));

            // Defence (4-3 base)
            AddGroup(TeamUnit.Defence, "Defensive Line",
                ("LE", "Left End"), ("DT", "Defensive Tackle"), ("NT", "Nose Tackle"), ("RE", "Right End"));
            AddGroup(TeamUnit.Defence, "Linebackers",
                ("SLB", "Strong-side Linebacker"), ("MLB", "Middle Linebacker"), ("WLB", "Weak-side Linebacker"));
            AddGroup(TeamUnit.Defence, "Defensive Backs",
                ("LCB", "Left Cornerback"), ("RCB", "Right Cornerback"), ("FS", "Free Safety"), ("SS", "Strong Safety"));

            // Special teams
            AddGroup(TeamUnit.SpecialTeams, "Specialists",
                ("K", "Kicker"), ("P", "Punter"), ("LS", "Long Snapper"), ("H", "Holder"),
                ("KR", "Kick Returner"), ("PR", "Punt Returner"));

            void AddGroup(TeamUnit unit, string name, params (string Code, string Name)[] positions)
            {
                var group = new PositionGroup
                {
                    Team = team,
                    Unit = unit,
                    Name = name,
                    SortOrder = ++groupOrder,
                    Positions = new List<Position>()
                };

                foreach (var position in positions)
                {
                    group.Positions.Add(new Position
                    {
                        Team = team,
                        Code = position.Code,
                        Name = position.Name,
                        SortOrder = ++positionOrder
                    });
                }

                context.PositionGroups.Add(group);
            }
        }
    }
}
