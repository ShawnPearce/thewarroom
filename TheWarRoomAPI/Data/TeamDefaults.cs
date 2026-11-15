using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Data
{
    // What every team starts with: a standard set of position groups and depth-chart positions,
    // a playbook for each unit holding the base formations, and the team's own entry in the film library.
    // Used when a team registers and when the demo teams are created, so both get the same setup.
    // Coaches can change all of it afterwards.
    //
    // The standard set depends on the game the team plays. Most positions are the same in both games.
    // American football alone has the tight end and two safeties; Canadian football, with its twelfth player,
    // has two more slotbacks, and two defensive halfbacks and one safety behind them.
    public static class TeamDefaults
    {
        public static void AddTo(Team team, AppDbContext context, DateTime now)
        {
            AddGroupsAndPositions(team, new List<PositionGroup>(), context);
            AddPlaybooks(team, new List<Playbook>(), context, now);

            // Games on film are played between two "teams on film"; this one stands for the team itself
            context.ScoutedTeams.Add(new ScoutedTeam { Team = team, Name = team.Name, IsOwnTeam = true });
        }

        // Called when the head coach has changed the game the team plays (team.Rules is already the new game).
        // The team is given what that game needs and it does not have yet: the game's standard positions,
        // and the game's three playbooks with their base formations. Nothing is removed or changed,
        // so a team that goes back to a game finds everything as it left it. The caller saves.
        public static async Task AddWhatTheGameNeedsAsync(Team team, AppDbContext context, DateTime now)
        {
            var groups = await context.PositionGroups
                .Where(g => g.TeamId == team.Id)
                .Include(g => g.Positions)
                .ToListAsync();

            var playbooks = await context.Playbooks
                .Where(p => p.TeamId == team.Id)
                .ToListAsync();

            AddGroupsAndPositions(team, groups, context);
            AddPlaybooks(team, playbooks, context, now);
        }

        // One playbook per unit for the team's game, each starting with a copy of that game's base formations.
        // A playbook the team already has for the game is left alone.
        private static void AddPlaybooks(Team team, List<Playbook> existing, AppDbContext context, DateTime now)
        {
            var templates = FormationTemplates.For(team.Rules);

            AddPlaybook(TeamUnit.Offence, "Offence playbook");
            AddPlaybook(TeamUnit.Defence, "Defence playbook");
            AddPlaybook(TeamUnit.SpecialTeams, "Special teams playbook");

            void AddPlaybook(TeamUnit unit, string name)
            {
                if (existing.Any(p => p.Unit == unit && p.Rules == team.Rules))
                {
                    return;
                }

                var playbook = new Playbook
                {
                    Team = team,
                    Unit = unit,
                    Rules = team.Rules,
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

        // The standard groups and positions of the team's game. What the team already has is left alone:
        // a group is found by its name, a position by its code.
        private static void AddGroupsAndPositions(Team team, List<PositionGroup> existing, AppDbContext context)
        {
            // New groups and positions go after the ones the team has
            int groupOrder = existing.Select(g => g.SortOrder).DefaultIfEmpty(0).Max();
            int positionOrder = existing.SelectMany(g => g.Positions ?? new List<Position>()).Select(p => p.SortOrder).DefaultIfEmpty(0).Max();

            var codesInUse = existing
                .SelectMany(g => g.Positions ?? new List<Position>())
                .Select(p => p.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Offence
            AddGroup(TeamUnit.Offence, "Quarterbacks", Both("QB", "Quarterback"));
            AddGroup(TeamUnit.Offence, "Running Backs", Both("RB", "Running Back"), Both("FB", "Fullback"));
            AddGroup(TeamUnit.Offence, "Receivers",
                Both("X", "X Receiver"), Both("Y", "Y Receiver"), Both("Z", "Z Receiver"),
                CanadianOnly("W", "W Slotback"), CanadianOnly("R", "R Slotback"));
            AddGroup(TeamUnit.Offence, "Tight Ends", AmericanOnly("TE", "Tight End"));
            AddGroup(TeamUnit.Offence, "Offensive Line",
                Both("LT", "Left Tackle"), Both("LG", "Left Guard"), Both("C", "Centre"), Both("RG", "Right Guard"), Both("RT", "Right Tackle"));

            // Defence (4-3 base)
            AddGroup(TeamUnit.Defence, "Defensive Line",
                Both("LE", "Left End"), Both("DT", "Defensive Tackle"), Both("NT", "Nose Tackle"), Both("RE", "Right End"));
            AddGroup(TeamUnit.Defence, "Linebackers",
                Both("SLB", "Strong-side Linebacker"), Both("MLB", "Middle Linebacker"), Both("WLB", "Weak-side Linebacker"));
            AddGroup(TeamUnit.Defence, "Defensive Backs",
                Both("LCB", "Left Cornerback"), Both("RCB", "Right Cornerback"),
                AmericanOnly("FS", "Free Safety"), AmericanOnly("SS", "Strong Safety"),
                CanadianOnly("LHB", "Left Halfback"), CanadianOnly("RHB", "Right Halfback"), CanadianOnly("S", "Safety"));

            // Special teams
            AddGroup(TeamUnit.SpecialTeams, "Specialists",
                Both("K", "Kicker"), Both("P", "Punter"), Both("LS", "Long Snapper"), Both("H", "Holder"),
                Both("KR", "Kick Returner"), Both("PR", "Punt Returner"));

            void AddGroup(TeamUnit unit, string name, params (string Code, string Name, FootballRules? Rules)[] positions)
            {
                // The positions of this group that the team's game has and the team does not
                var missing = positions
                    .Where(p => p.Rules == null || p.Rules == team.Rules)
                    .Where(p => !codesInUse.Contains(p.Code))
                    .ToList();
                if (missing.Count == 0)
                {
                    return;   // nothing to add: a group with no position in this game is not made for it
                }

                var group = existing.FirstOrDefault(g => g.Unit == unit && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = new PositionGroup
                    {
                        Team = team,
                        Unit = unit,
                        Name = name,
                        SortOrder = ++groupOrder,
                        Positions = new List<Position>()
                    };
                    context.PositionGroups.Add(group);
                }

                group.Positions ??= new List<Position>();
                foreach (var position in missing)
                {
                    group.Positions.Add(new Position
                    {
                        Team = team,
                        Code = position.Code,
                        Name = position.Name,
                        Rules = position.Rules,
                        SortOrder = ++positionOrder
                    });
                    codesInUse.Add(position.Code);
                }
            }
        }

        // A position both games have, and a position only one of them has
        private static (string Code, string Name, FootballRules? Rules) Both(string code, string name)
        {
            return (code, name, null);
        }

        private static (string Code, string Name, FootballRules? Rules) AmericanOnly(string code, string name)
        {
            return (code, name, FootballRules.American);
        }

        private static (string Code, string Name, FootballRules? Rules) CanadianOnly(string code, string name)
        {
            return (code, name, FootballRules.Canadian);
        }
    }
}
