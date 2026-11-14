using System.Text.Json;

namespace TheWarRoomAPI.Diagrams
{
    // A diagram arrives from the browser as JSON text, so it is treated like any other user input:
    // it is parsed, checked against the limits below, and written out again in a standard form
    // before it is stored. Anything that is not part of the format is dropped on the way.
    public static class DiagramValidator
    {
        public const double HalfFieldWidth = 26.67;   // the field is 53 1/3 yards wide

        private const int MaxJsonLength = 64 * 1024;
        private const int MaxPlayersPerSide = 11;
        private const int MaxPathsPerPlayer = 3;
        private const int MaxPointsPerPath = 12;
        private const int MaxDrawings = 20;
        private const int MaxLabelLength = 4;
        private const int MaxTextLength = 60;

        private static readonly string[] Units = { "Offence", "Defence", "SpecialTeams" };
        private static readonly string[] Teams = { "own", "opp" };
        private static readonly string[] Shapes = { "circle", "square", "triangle" };
        private static readonly string[] PathKinds = { "route", "block", "motion" };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Returns the diagram in its standard form, or throws InvalidOperationException saying what is wrong.
        // A formation keeps its players only: routes, blocks, motion and text belong to plays.
        public static string Normalize(string? json, string unit, bool isFormation)
        {
            var diagram = Parse(json);

            diagram.Unit = unit;   // the unit comes from the playbook, not from the browser
            Check(diagram);

            if (isFormation)
            {
                foreach (var player in diagram.Players)
                {
                    player.Paths.Clear();
                }
                diagram.Drawings.Clear();
            }

            return Serialize(diagram);
        }

        public static string Serialize(DiagramDocument diagram)
        {
            return JsonSerializer.Serialize(diagram, JsonOptions);
        }

        public static DiagramDocument Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("The diagram is empty.");
            }
            if (json.Length > MaxJsonLength)
            {
                throw new InvalidOperationException("The diagram is too large.");
            }

            try
            {
                return JsonSerializer.Deserialize<DiagramDocument>(json, JsonOptions)
                    ?? throw new InvalidOperationException("The diagram is empty.");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The diagram could not be read.");
            }
        }

        private static void Check(DiagramDocument diagram)
        {
            if (diagram.SchemaVersion != 1)
            {
                throw new InvalidOperationException("This diagram was made with a version of the designer that is not supported.");
            }
            if (!Units.Contains(diagram.Unit))
            {
                throw new InvalidOperationException("The diagram's unit is not valid.");
            }

            // The part of the field that is shown
            diagram.View ??= new DiagramView();
            double height = diagram.View.YMax - diagram.View.YMin;
            if (!IsNumber(diagram.View.YMin) || !IsNumber(diagram.View.YMax)
                || diagram.View.YMin < -40 || diagram.View.YMax > 70 || height < 15 || height > 90)
            {
                throw new InvalidOperationException("The part of the field shown by the diagram is not valid.");
            }

            if (!IsNumber(diagram.BallX) || Math.Abs(diagram.BallX) > HalfFieldWidth)
            {
                throw new InvalidOperationException("The ball is outside the field.");
            }
            diagram.BallX = Round(diagram.BallX);

            CheckPlayers(diagram);
            CheckDrawings(diagram);
        }

        private static void CheckPlayers(DiagramDocument diagram)
        {
            diagram.Players ??= new List<DiagramPlayer>();

            if (!diagram.Players.Any(p => p.Team == "own"))
            {
                throw new InvalidOperationException("A diagram needs at least one of your own players.");
            }
            if (diagram.Players.Count(p => p.Team == "own") > MaxPlayersPerSide
                || diagram.Players.Count(p => p.Team == "opp") > MaxPlayersPerSide)
            {
                throw new InvalidOperationException("A side can have at most 11 players on the field.");
            }

            var ids = new HashSet<string>();
            foreach (var player in diagram.Players)
            {
                if (string.IsNullOrWhiteSpace(player.Id) || player.Id.Length > 20 || !ids.Add(player.Id))
                {
                    throw new InvalidOperationException("Every player in a diagram needs its own id.");
                }
                if (!Teams.Contains(player.Team) || !Shapes.Contains(player.Shape))
                {
                    throw new InvalidOperationException("A player in the diagram has a type that is not supported.");
                }

                player.Label = (player.Label ?? string.Empty).Trim();
                if (player.Label.Length > MaxLabelLength)
                {
                    throw new InvalidOperationException($"A player label can have at most {MaxLabelLength} characters.");
                }

                if (!IsOnField(player.X, player.Y, diagram.View))
                {
                    throw new InvalidOperationException($"Player {player.Label} is outside the part of the field that is shown.");
                }
                player.X = Round(player.X);
                player.Y = Round(player.Y);

                player.Paths ??= new List<DiagramPath>();
                if (player.Paths.Count > MaxPathsPerPlayer)
                {
                    throw new InvalidOperationException($"A player can have at most {MaxPathsPerPlayer} routes, blocks or motions.");
                }

                foreach (var path in player.Paths)
                {
                    if (!PathKinds.Contains(path.Kind))
                    {
                        throw new InvalidOperationException("A drawing in the diagram has a type that is not supported.");
                    }
                    if (path.Pts == null || path.Pts.Count == 0 || path.Pts.Count > MaxPointsPerPath)
                    {
                        throw new InvalidOperationException($"A route needs between 1 and {MaxPointsPerPath} points.");
                    }

                    foreach (var point in path.Pts)
                    {
                        // Each point is an [x, y] offset from the player
                        if (point == null || point.Length != 2 || !IsOnField(player.X + point[0], player.Y + point[1], diagram.View))
                        {
                            throw new InvalidOperationException($"A route of player {player.Label} goes outside the part of the field that is shown.");
                        }
                        point[0] = Round(point[0]);
                        point[1] = Round(point[1]);
                    }
                }
            }
        }

        private static void CheckDrawings(DiagramDocument diagram)
        {
            diagram.Drawings ??= new List<DiagramDrawing>();
            if (diagram.Drawings.Count > MaxDrawings)
            {
                throw new InvalidOperationException($"A diagram can have at most {MaxDrawings} text labels.");
            }

            var ids = new HashSet<string>();
            foreach (var drawing in diagram.Drawings)
            {
                if (string.IsNullOrWhiteSpace(drawing.Id) || drawing.Id.Length > 20 || !ids.Add(drawing.Id))
                {
                    throw new InvalidOperationException("Every text label in a diagram needs its own id.");
                }
                if (drawing.Type != "text")
                {
                    throw new InvalidOperationException("A drawing in the diagram has a type that is not supported.");
                }

                drawing.Text = (drawing.Text ?? string.Empty).Trim();
                if (drawing.Text.Length == 0 || drawing.Text.Length > MaxTextLength)
                {
                    throw new InvalidOperationException($"A text label needs between 1 and {MaxTextLength} characters.");
                }

                if (!IsOnField(drawing.X, drawing.Y, diagram.View))
                {
                    throw new InvalidOperationException("A text label is outside the part of the field that is shown.");
                }
                drawing.X = Round(drawing.X);
                drawing.Y = Round(drawing.Y);
            }
        }

        private static bool IsOnField(double x, double y, DiagramView view)
        {
            // A little slack lets something sit exactly on the edge
            const double slack = 0.01;
            return IsNumber(x) && IsNumber(y)
                && Math.Abs(x) <= HalfFieldWidth + slack
                && y >= view.YMin - slack && y <= view.YMax + slack;
        }

        private static bool IsNumber(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Round(double value)
        {
            return Math.Round(value, 2);
        }
    }
}
