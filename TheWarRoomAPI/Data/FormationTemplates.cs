using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Data
{
    // The base formations a team's playbooks start with, for each of the two games:
    // 11 players a side for American football, 12 a side on the wider field for Canadian football.
    // Each one is a list of players with a label and a position in yards:
    // x from the centre of the field, y from the line of scrimmage (negative is behind it).
    // A team's copies can be changed, removed, or saved under a new name afterwards.
    public static class FormationTemplates
    {
        public class Template
        {
            public TeamUnit Unit { get; set; }
            public string Name { get; set; } = string.Empty;
            public DiagramDocument Diagram { get; set; } = new DiagramDocument();
        }

        // The five offensive linemen are the same in every offensive formation, in both games
        private static readonly (string Label, double X, double Y)[] OffensiveLine =
        {
            ("LT", -4, -1), ("LG", -2, -1), ("C", 0, -1), ("RG", 2, -1), ("RT", 4, -1)
        };

        public static List<Template> For(FootballRules rules)
        {
            return rules == FootballRules.Canadian ? Canadian() : American();
        }

        // ---------- American football: 11 a side, the field 53 1/3 yards wide ----------

        private static List<Template> American()
        {
            const FootballRules rules = FootballRules.American;

            return new List<Template>
            {
                // ----- Offence: lines up below the line of scrimmage -----
                Offence(rules, "I-Form",
                    ("TE", 6, -1), ("X", -22, -1), ("Z", 20, -2), ("QB", 0, -2.5), ("FB", 0, -5), ("RB", 0, -7.5)),
                Offence(rules, "Singleback",
                    ("TE", 6, -1), ("X", -22, -1), ("Y", -14, -2), ("Z", 22, -2), ("QB", 0, -2.5), ("RB", 0, -7)),
                Offence(rules, "Shotgun Spread",
                    ("X", -22, -1), ("H", -13, -2), ("Y", 13, -2), ("Z", 22, -1), ("QB", 0, -5), ("RB", 2.5, -5)),
                Offence(rules, "Shotgun Trips",
                    ("X", -22, -1), ("H", 10, -2), ("Y", 16, -2), ("Z", 22, -1), ("QB", 0, -5), ("RB", -2.5, -5)),
                Offence(rules, "Pistol",
                    ("TE", 6, -1), ("X", -22, -1), ("Y", -13, -2), ("Z", 22, -2), ("QB", 0, -4), ("RB", 0, -7)),

                // ----- Defence: lines up above the line of scrimmage, seen from the offence's side -----
                Defence(rules, "4-3",
                    ("RE", -5, 1), ("NT", -1.5, 1), ("DT", 1.5, 1), ("LE", 5, 1),
                    ("WLB", -6, 5), ("MLB", 0, 5), ("SLB", 6, 5),
                    ("RCB", -20, 6), ("LCB", 20, 6), ("FS", -6, 13), ("SS", 8, 11)),
                Defence(rules, "3-4",
                    ("RE", -4, 1), ("NT", 0, 1), ("LE", 4, 1),
                    ("OLB", -8, 2), ("ILB", -2.5, 5), ("ILB", 2.5, 5), ("OLB", 8, 2),
                    ("RCB", -20, 6), ("LCB", 20, 6), ("FS", -6, 13), ("SS", 8, 11)),
                Defence(rules, "Nickel",
                    ("RE", -5, 1), ("NT", -1.5, 1), ("DT", 1.5, 1), ("LE", 5, 1),
                    ("WLB", -3, 5), ("MLB", 3, 5), ("NB", -12, 4),
                    ("RCB", -20, 6), ("LCB", 20, 6), ("FS", -6, 13), ("SS", 8, 12)),

                // ----- Special teams: each shows as much of the field as the kick needs -----
                SpecialTeams(rules, "Kickoff", -12, 48,
                    ("K", 0, -8),
                    ("L5", -24, -2), ("L4", -19, -2), ("L3", -14, -2), ("L2", -9, -2), ("L1", -4, -2),
                    ("R1", 4, -2), ("R2", 9, -2), ("R3", 14, -2), ("R4", 19, -2), ("R5", 24, -2)),
                SpecialTeams(rules, "Kick Return", -8, 52,
                    ("F1", -20, 12), ("F2", -10, 12), ("F3", 0, 12), ("F4", 10, 12), ("F5", 20, 12),
                    ("M1", -15, 25), ("M2", -5, 25), ("M3", 5, 25), ("M4", 15, 25),
                    ("KR", -7, 45), ("KR", 7, 45)),
                SpecialTeams(rules, "Punt", -18, 27,
                    ("LS", 0, -1), ("LG", -2, -1), ("RG", 2, -1), ("LT", -4, -1), ("RT", 4, -1),
                    ("LW", -6, -2), ("RW", 6, -2), ("GL", -22, -1), ("GR", 22, -1), ("PP", 2, -6), ("P", 0, -14)),
                SpecialTeams(rules, "Punt Return", -18, 42,
                    ("D1", -5, 1), ("D2", -3, 1), ("D3", -1, 1), ("D4", 1, 1), ("D5", 3, 1), ("D6", 5, 1),
                    ("B1", -3, 5), ("B2", 3, 5), ("J", -22, 2), ("J", 22, 2), ("PR", 0, 38)),
                SpecialTeams(rules, "Field Goal", -14, 16,
                    ("LS", 0, -1), ("LG", -2, -1), ("RG", 2, -1), ("LT", -4, -1), ("RT", 4, -1),
                    ("LE", -6, -1), ("RE", 6, -1), ("LW", -7.5, -2), ("RW", 7.5, -2), ("H", -0.5, -7), ("K", 1.5, -9))
            };
        }

        // ---------- Canadian football: 12 a side, the field 65 yards wide ----------
        //
        // The twelfth player on offence is another receiver: the tight end gives way to slotbacks (W, Y and R)
        // between the two wide receivers (X and Z). On defence he is a fifth defensive back: two cornerbacks,
        // two defensive halfbacks and one safety. The defence lines up a yard off the ball.

        private static List<Template> Canadian()
        {
            const FootballRules rules = FootballRules.Canadian;

            return new List<Template>
            {
                // ----- Offence -----
                Offence(rules, "I-Form",
                    ("Y", 6, -1), ("X", -27, -1), ("W", -15, -2), ("Z", 26, -2), ("QB", 0, -2.5), ("FB", 0, -5), ("RB", 0, -7.5)),
                Offence(rules, "Singleback",
                    ("Y", 6, -1), ("X", -27, -1), ("W", -15, -2), ("R", 15, -2), ("Z", 27, -2), ("QB", 0, -2.5), ("RB", 0, -7)),
                Offence(rules, "Shotgun Spread",
                    ("X", -27, -1), ("W", -16, -2), ("Y", 10, -2), ("R", 18, -2), ("Z", 27, -1), ("QB", 0, -5), ("RB", 2.5, -5)),
                Offence(rules, "Shotgun Trips",
                    ("X", -27, -1), ("W", -9, -2), ("Y", 11, -2), ("R", 17, -2), ("Z", 24, -1), ("QB", 0, -5), ("RB", -2.5, -5)),
                Offence(rules, "Pistol",
                    ("Y", 6, -1), ("X", -27, -1), ("W", -15, -2), ("R", 15, -2), ("Z", 27, -2), ("QB", 0, -4), ("RB", 0, -7)),

                // ----- Defence -----
                Defence(rules, "4-3",
                    ("RE", -5, 1.5), ("NT", -1.5, 1.5), ("DT", 1.5, 1.5), ("LE", 5, 1.5),
                    ("WLB", -7, 5), ("MLB", 0, 5), ("SLB", 7, 5),
                    ("RCB", -25, 6), ("LCB", 25, 6), ("RHB", -14, 7), ("LHB", 14, 7), ("S", 0, 14)),
                Defence(rules, "3-4",
                    ("RE", -4, 1.5), ("NT", 0, 1.5), ("LE", 4, 1.5),
                    ("OLB", -8, 2.5), ("ILB", -2.5, 5), ("ILB", 2.5, 5), ("OLB", 8, 2.5),
                    ("RCB", -25, 6), ("LCB", 25, 6), ("RHB", -14, 7), ("LHB", 14, 7), ("S", 0, 14)),
                Defence(rules, "Dime",
                    ("RE", -5, 1.5), ("NT", -1.5, 1.5), ("DT", 1.5, 1.5), ("LE", 5, 1.5),
                    ("WLB", -3, 5), ("MLB", 3, 5), ("DB", 9, 6),
                    ("RCB", -25, 6), ("LCB", 25, 6), ("RHB", -14, 7), ("LHB", 16, 8), ("S", 0, 14)),

                // ----- Special teams -----
                SpecialTeams(rules, "Kickoff", -12, 48,
                    ("K", 0, -8),
                    ("L6", -30, -2), ("L5", -25, -2), ("L4", -20, -2), ("L3", -15, -2), ("L2", -10, -2), ("L1", -5, -2),
                    ("R1", 5, -2), ("R2", 11, -2), ("R3", 17, -2), ("R4", 23, -2), ("R5", 29, -2)),
                SpecialTeams(rules, "Kick Return", -8, 52,
                    ("F1", -24, 12), ("F2", -12, 12), ("F3", 0, 12), ("F4", 12, 12), ("F5", 24, 12),
                    ("M1", -20, 25), ("M2", -10, 25), ("M3", 0, 25), ("M4", 10, 25), ("M5", 20, 25),
                    ("KR", -9, 45), ("KR", 9, 45)),
                SpecialTeams(rules, "Punt", -18, 27,
                    ("LS", 0, -1), ("LG", -2, -1), ("RG", 2, -1), ("LT", -4, -1), ("RT", 4, -1),
                    ("LW", -6, -2), ("RW", 6, -2), ("GL", -27, -1), ("GR", 27, -1),
                    ("PP", -2, -6), ("PP", 2, -6), ("P", 0, -14)),
                SpecialTeams(rules, "Punt Return", -18, 42,
                    ("D1", -5, 1.5), ("D2", -3, 1.5), ("D3", -1, 1.5), ("D4", 1, 1.5), ("D5", 3, 1.5), ("D6", 5, 1.5),
                    ("B1", -4, 5), ("B2", 4, 5), ("J", -27, 2), ("J", 27, 2), ("PR", -8, 38), ("PR", 8, 38)),
                SpecialTeams(rules, "Field Goal", -14, 16,
                    ("LS", 0, -1), ("LG", -2, -1), ("RG", 2, -1), ("LT", -4, -1), ("RT", 4, -1),
                    ("LE", -6, -1), ("RE", 6, -1), ("LW", -7.5, -2), ("RW", 7.5, -2),
                    ("UB", -3.5, -4.5), ("H", -0.5, -7), ("K", 1.5, -9))
            };
        }

        private static Template Offence(FootballRules rules, string name, params (string Label, double X, double Y)[] skillPlayers)
        {
            var players = OffensiveLine.Concat(skillPlayers).ToArray();
            var template = Build(rules, TeamUnit.Offence, name, -12, 18, "circle", players);

            // By convention the centre is drawn as a square
            template.Diagram.Players.First(p => p.Label == "C").Shape = "square";
            return template;
        }

        private static Template Defence(FootballRules rules, string name, params (string Label, double X, double Y)[] players)
        {
            return Build(rules, TeamUnit.Defence, name, -8, 22, "triangle", players);
        }

        private static Template SpecialTeams(FootballRules rules, string name, double yMin, double yMax,
            params (string Label, double X, double Y)[] players)
        {
            return Build(rules, TeamUnit.SpecialTeams, name, yMin, yMax, "circle", players);
        }

        private static Template Build(FootballRules rules, TeamUnit unit, string name, double yMin, double yMax, string shape,
            (string Label, double X, double Y)[] players)
        {
            var diagram = new DiagramDocument
            {
                Unit = unit.ToString(),
                Rules = rules.ToString(),
                View = new DiagramView { YMin = yMin, YMax = yMax }
            };

            foreach (var player in players)
            {
                // The id is the label in lower case; a second player with the same label gets a number ("ilb", "ilb2")
                string id = player.Label.ToLowerInvariant();
                int copy = 2;
                while (diagram.Players.Any(p => p.Id == id))
                {
                    id = $"{player.Label.ToLowerInvariant()}{copy++}";
                }

                diagram.Players.Add(new DiagramPlayer
                {
                    Id = id,
                    Team = "own",
                    Label = player.Label,
                    Shape = shape,
                    X = player.X,
                    Y = player.Y
                });
            }

            return new Template { Unit = unit, Name = name, Diagram = diagram };
        }
    }
}
