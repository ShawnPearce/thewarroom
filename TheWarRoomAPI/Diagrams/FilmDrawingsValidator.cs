using System.Text.Json;

namespace TheWarRoomAPI.Diagrams
{
    // The drawings on a play arrive from the browser as JSON text, so they are treated like any other user input:
    // parsed, checked against the limits below, and written out again in a standard form before they are stored.
    // Anything that is not part of the format is dropped on the way.
    public static class FilmDrawingsValidator
    {
        public const int MaxStops = 5;
        public const int MaxMarksPerStop = 12;
        public const int MinHoldMs = 1000;      // a stop shorter than a second cannot be read
        public const int MaxHoldMs = 10_000;
        public const int MaxTextLength = 40;

        private const int MaxJsonLength = 32 * 1024;
        private const double ShortestArrow = 0.02;   // of the picture's width or height

        private static readonly string[] Types = { "arrow", "text" };
        private static readonly string[] Colours = { "yellow", "red", "white" };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Returns the drawings in their standard form, or null when there are none.
        // Throws InvalidOperationException saying what is wrong.
        // startMs and endMs are where the play lies in its video: every stop has to be inside it.
        public static string? Normalize(string? json, int startMs, int endMs)
        {
            var drawings = Parse(json);
            if (drawings == null || drawings.Stops.Count == 0)
            {
                return null;
            }

            if (drawings.SchemaVersion != 1)
            {
                throw new InvalidOperationException("These drawings were made with a version of the editor that is not supported.");
            }

            if (drawings.Stops.Count > MaxStops)
            {
                throw new InvalidOperationException($"A play can have at most {MaxStops} stops with drawings.");
            }

            foreach (var stop in drawings.Stops)
            {
                Check(stop, startMs, endMs);
            }

            drawings.Stops = drawings.Stops.OrderBy(s => s.AtMs).ToList();

            // Business rule: the video stops once at a moment, so two stops cannot share it
            for (int i = 1; i < drawings.Stops.Count; i++)
            {
                if (drawings.Stops[i].AtMs == drawings.Stops[i - 1].AtMs)
                {
                    throw new InvalidOperationException("Two stops cannot be at the same moment of the play.");
                }
            }

            return JsonSerializer.Serialize(drawings, JsonOptions);
        }

        // What is left of a play's drawings after the play was trimmed: the stops that still lie inside it.
        // Drawings are stored in their standard form, so nothing else needs checking here.
        public static string? KeepInside(string? storedJson, int startMs, int endMs)
        {
            var drawings = Parse(storedJson);
            if (drawings == null)
            {
                return null;
            }

            drawings.Stops = drawings.Stops.Where(s => s.AtMs >= startMs && s.AtMs <= endMs).ToList();

            return drawings.Stops.Count == 0 ? null : JsonSerializer.Serialize(drawings, JsonOptions);
        }

        private static FilmDrawings? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            if (json.Length > MaxJsonLength)
            {
                throw new InvalidOperationException("The drawings are too large.");
            }

            try
            {
                return JsonSerializer.Deserialize<FilmDrawings>(json, JsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The drawings could not be read.");
            }
        }

        private static void Check(FilmStop stop, int startMs, int endMs)
        {
            // Business rule: a drawing belongs to a moment inside its play
            if (stop.AtMs < startMs || stop.AtMs > endMs)
            {
                throw new InvalidOperationException("A drawing has to be on a moment inside the play.");
            }

            if (stop.HoldMs < MinHoldMs || stop.HoldMs > MaxHoldMs)
            {
                throw new InvalidOperationException($"The video can stop for {MinHoldMs / 1000} to {MaxHoldMs / 1000} seconds.");
            }

            if (stop.Marks.Count == 0)
            {
                throw new InvalidOperationException("A stop needs at least one arrow or text.");
            }
            if (stop.Marks.Count > MaxMarksPerStop)
            {
                throw new InvalidOperationException($"A stop can have at most {MaxMarksPerStop} arrows and texts.");
            }

            foreach (var mark in stop.Marks)
            {
                Check(mark);
            }
        }

        private static void Check(FilmMark mark)
        {
            if (!Types.Contains(mark.Type))
            {
                throw new InvalidOperationException("A drawing is an arrow or a text.");
            }
            if (!Colours.Contains(mark.Colour))
            {
                throw new InvalidOperationException("A drawing is yellow, red or white.");
            }

            mark.X1 = OnThePicture(mark.X1);
            mark.Y1 = OnThePicture(mark.Y1);

            if (mark.Type == "arrow")
            {
                mark.X2 = OnThePicture(mark.X2);
                mark.Y2 = OnThePicture(mark.Y2);
                mark.Text = null;

                if (Math.Abs(mark.X2 - mark.X1) < ShortestArrow && Math.Abs(mark.Y2 - mark.Y1) < ShortestArrow)
                {
                    throw new InvalidOperationException("An arrow is too short to be seen.");
                }
            }
            else
            {
                mark.X2 = 0;
                mark.Y2 = 0;
                mark.Text = mark.Text?.Trim();

                if (string.IsNullOrEmpty(mark.Text))
                {
                    throw new InvalidOperationException("A text on a drawing cannot be empty.");
                }
                if (mark.Text.Length > MaxTextLength)
                {
                    throw new InvalidOperationException($"A text on a drawing can't exceed {MaxTextLength} characters.");
                }
            }
        }

        // A position as a part of the picture, from 0 to 1, kept to four decimals
        private static double OnThePicture(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
            {
                throw new InvalidOperationException("A drawing has to be on the picture.");
            }

            return Math.Round(value, 4);
        }
    }
}
