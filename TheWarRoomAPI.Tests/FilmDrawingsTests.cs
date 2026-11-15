using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheWarRoomAPI.Diagrams;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Tests
{
    // The arrows and text a coach draws on a play, and the moments the video stops to show them
    public class FilmDrawingsTests : IDisposable
    {
        private readonly TestWorld _world = new TestWorld();

        public void Dispose()
        {
            _world.Dispose();
        }

        // One stop, with an arrow and a text unless the test gives its own marks
        private static string Drawings(int atMs = 12_000, int holdMs = 3000, string? marks = null)
        {
            marks ??= """
                { "type": "arrow", "x1": 0.2, "y1": 0.5, "x2": 0.6, "y2": 0.3, "colour": "yellow" },
                { "type": "text", "x1": 0.5, "y1": 0.1, "text": "Watch the safety", "colour": "white" }
                """;

            return $$"""{ "schemaVersion": 1, "stops": [ { "atMs": {{atMs}}, "holdMs": {{holdMs}}, "marks": [ {{marks}} ] } ] }""";
        }

        private static FilmDrawings Read(string? json)
        {
            return JsonSerializer.Deserialize<FilmDrawings>(json!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }

        // ---------- The format ----------

        [Fact]
        public void Drawings_AreStoredInAStandardForm()
        {
            // The play runs from 0:10 to 0:18 in its video
            string? stored = FilmDrawingsValidator.Normalize(Drawings(), startMs: 10_000, endMs: 18_000);

            var drawings = Read(stored);
            var stop = Assert.Single(drawings.Stops);
            Assert.Equal(12_000, stop.AtMs);
            Assert.Equal(3000, stop.HoldMs);
            Assert.Equal(2, stop.Marks.Count);

            var arrow = stop.Marks[0];
            Assert.Equal(("arrow", 0.2, 0.5, 0.6, 0.3, "yellow"), (arrow.Type, arrow.X1, arrow.Y1, arrow.X2, arrow.Y2, arrow.Colour));
            Assert.Null(arrow.Text);

            var text = stop.Marks[1];
            Assert.Equal(("text", 0.5, 0.1, "Watch the safety", "white"), (text.Type, text.X1, text.Y1, text.Text, text.Colour));

            // Storing what was stored changes nothing
            Assert.Equal(stored, FilmDrawingsValidator.Normalize(stored, 10_000, 18_000));
        }

        [Fact]
        public void AnythingThatIsNotPartOfTheFormat_IsDropped()
        {
            string withExtras = """
                { "schemaVersion": 1, "note": "<script>alert(1)</script>", "stops": [ { "atMs": 12000, "holdMs": 3000, "onclick": "x()",
                  "marks": [ { "type": "arrow", "x1": 0.123456789, "y1": 0.5, "x2": 0.6, "y2": 0.3, "colour": "red", "text": "not for arrows", "width": 900 } ] } ] }
                """;

            string? stored = FilmDrawingsValidator.Normalize(withExtras, 10_000, 18_000);

            Assert.DoesNotContain("script", stored);
            Assert.DoesNotContain("onclick", stored);
            Assert.DoesNotContain("width", stored);
            Assert.DoesNotContain("not for arrows", stored);
            Assert.Equal(0.1235, Read(stored).Stops[0].Marks[0].X1);   // positions are kept to four decimals
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("""{ "schemaVersion": 1, "stops": [] }""")]
        public void NoDrawings_AreStoredAsNothing(string? json)
        {
            Assert.Null(FilmDrawingsValidator.Normalize(json, 10_000, 18_000));
        }

        [Fact]
        public void Stops_AreKeptInTheOrderTheyCome()
        {
            string twoStops = """
                { "schemaVersion": 1, "stops": [
                  { "atMs": 16000, "holdMs": 2000, "marks": [ { "type": "text", "x1": 0.5, "y1": 0.5, "text": "Second", "colour": "red" } ] },
                  { "atMs": 11000, "holdMs": 2000, "marks": [ { "type": "text", "x1": 0.5, "y1": 0.5, "text": "First", "colour": "red" } ] } ] }
                """;

            var drawings = Read(FilmDrawingsValidator.Normalize(twoStops, 10_000, 18_000));

            Assert.Equal(new[] { 11_000, 16_000 }, drawings.Stops.Select(s => s.AtMs));
            Assert.Equal("First", drawings.Stops[0].Marks[0].Text);
        }

        [Theory]
        [InlineData(9_999)]     // before the play starts
        [InlineData(18_001)]    // after it ends
        public void ADrawing_HasToBeOnAMomentInsideThePlay(int atMs)
        {
            var error = Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(Drawings(atMs), 10_000, 18_000));
            Assert.Contains("inside the play", error.Message);

            // The first and the last moment of the play are inside it
            Assert.NotNull(FilmDrawingsValidator.Normalize(Drawings(10_000), 10_000, 18_000));
            Assert.NotNull(FilmDrawingsValidator.Normalize(Drawings(18_000), 10_000, 18_000));
        }

        [Theory]
        [InlineData(999)]
        [InlineData(10_001)]
        public void TheVideo_StopsForOneToTenSeconds(int holdMs)
        {
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(Drawings(holdMs: holdMs), 10_000, 18_000));

            Assert.NotNull(FilmDrawingsValidator.Normalize(Drawings(holdMs: FilmDrawingsValidator.MinHoldMs), 10_000, 18_000));
            Assert.NotNull(FilmDrawingsValidator.Normalize(Drawings(holdMs: FilmDrawingsValidator.MaxHoldMs), 10_000, 18_000));
        }

        [Theory]
        [InlineData("""{ "type": "circle", "x1": 0.5, "y1": 0.5, "x2": 0.6, "y2": 0.6, "colour": "yellow" }""")]   // not an arrow or a text
        [InlineData("""{ "type": "arrow", "x1": 0.5, "y1": 0.5, "x2": 0.6, "y2": 0.6, "colour": "#00ff00" }""")]   // not one of the three colours
        [InlineData("""{ "type": "arrow", "x1": 1.2, "y1": 0.5, "x2": 0.6, "y2": 0.6, "colour": "yellow" }""")]    // off the picture
        [InlineData("""{ "type": "arrow", "x1": 0.5, "y1": -0.1, "x2": 0.6, "y2": 0.6, "colour": "yellow" }""")]
        [InlineData("""{ "type": "arrow", "x1": 0.5, "y1": 0.5, "x2": 0.505, "y2": 0.505, "colour": "yellow" }""")] // too short to be seen
        [InlineData("""{ "type": "text", "x1": 0.5, "y1": 0.5, "text": "   ", "colour": "yellow" }""")]             // an empty text
        [InlineData("""{ "type": "text", "x1": 0.5, "y1": 0.5, "text": "This text is far too long to be read while the video is stopped", "colour": "yellow" }""")]
        public void AMarkThatIsNotAProperArrowOrText_IsRefused(string mark)
        {
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(Drawings(marks: mark), 10_000, 18_000));
        }

        [Fact]
        public void TheNumberOfStopsAndMarks_IsLimited_AndBrokenTextIsRefused()
        {
            const string arrow = """{ "type": "arrow", "x1": 0.2, "y1": 0.5, "x2": 0.6, "y2": 0.3, "colour": "yellow" }""";

            // More marks on one stop than can be taken in
            string tooManyMarks = string.Join(",", Enumerable.Repeat(arrow, FilmDrawingsValidator.MaxMarksPerStop + 1));
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(Drawings(marks: tooManyMarks), 10_000, 18_000));

            // More stops than a play has room for
            var stops = Enumerable.Range(0, FilmDrawingsValidator.MaxStops + 1)
                .Select(i => $$"""{ "atMs": {{10_000 + i * 1000}}, "holdMs": 2000, "marks": [ {{arrow}} ] }""");
            string tooManyStops = $$"""{ "schemaVersion": 1, "stops": [ {{string.Join(",", stops)}} ] }""";
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(tooManyStops, 10_000, 18_000));

            // Two stops at the same moment, a stop with nothing on it, another version, and text that is not JSON
            string sameMoment = $$"""{ "schemaVersion": 1, "stops": [ { "atMs": 12000, "holdMs": 2000, "marks": [ {{arrow}} ] }, { "atMs": 12000, "holdMs": 2000, "marks": [ {{arrow}} ] } ] }""";
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(sameMoment, 10_000, 18_000));
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize("""{ "schemaVersion": 1, "stops": [ { "atMs": 12000, "holdMs": 2000, "marks": [] } ] }""", 10_000, 18_000));
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(Drawings().Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), 10_000, 18_000));
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize("arrows everywhere", 10_000, 18_000));
            Assert.Throws<InvalidOperationException>(() => FilmDrawingsValidator.Normalize(new string('x', 40_000), 10_000, 18_000));
        }

        // ---------- On a play ----------

        [Fact]
        public async Task DrawingsAreSavedOnAPlay_AndTakenAwayAgain()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);

            using var context = _world.NewContext();
            var clips = _world.Clips(context);

            Assert.True(await clips.SaveAnnotationsAsync(headCoach, clipId, new SaveAnnotationsDto { AnnotationsJson = Drawings() }));

            var clip = await clips.GetClipAsync(headCoach, clipId);
            Assert.Equal(12_000, Read(clip!.AnnotationsJson).Stops.Single().AtMs);

            // A drawing outside the play is refused, and what was there stays
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => clips.SaveAnnotationsAsync(headCoach, clipId, new SaveAnnotationsDto { AnnotationsJson = Drawings(atMs: 30_000) }));
            Assert.NotNull((await clips.GetClipAsync(headCoach, clipId))!.AnnotationsJson);

            // Sending none takes the drawings away
            Assert.True(await clips.SaveAnnotationsAsync(headCoach, clipId, new SaveAnnotationsDto { AnnotationsJson = null }));
            Assert.Null((await clips.GetClipAsync(headCoach, clipId))!.AnnotationsJson);
        }

        [Fact]
        public async Task WhenAPlayIsTrimmed_ADrawingOnAMomentThatWasCutOff_GoesWithIt()
        {
            var headCoach = await _world.AddTeamAsync();
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);

            string twoStops = """
                { "schemaVersion": 1, "stops": [
                  { "atMs": 11000, "holdMs": 2000, "marks": [ { "type": "text", "x1": 0.5, "y1": 0.5, "text": "Early", "colour": "red" } ] },
                  { "atMs": 16000, "holdMs": 2000, "marks": [ { "type": "text", "x1": 0.5, "y1": 0.5, "text": "Late", "colour": "red" } ] } ] }
                """;

            using var context = _world.NewContext();
            var clips = _world.Clips(context);
            await clips.SaveAnnotationsAsync(headCoach, clipId, new SaveAnnotationsDto { AnnotationsJson = twoStops });

            // The play now starts at 0:13: the stop at 0:11 is no longer part of it
            await clips.UpdateAsync(headCoach, clipId, new SaveClipDto { VideoId = film.VideoId, StartMs = 13_000, EndMs = 18_000 });

            var stop = Assert.Single(Read((await clips.GetClipAsync(headCoach, clipId))!.AnnotationsJson).Stops);
            Assert.Equal("Late", stop.Marks[0].Text);

            // Trimmed until no drawing is left
            await clips.UpdateAsync(headCoach, clipId, new SaveClipDto { VideoId = film.VideoId, StartMs = 13_000, EndMs = 15_000 });
            Assert.Null((await clips.GetClipAsync(headCoach, clipId))!.AnnotationsJson);
        }

        [Fact]
        public async Task DrawingsGoIntoCutupsWithTheirPlay_AndPlayersGetThemWithTheFilm()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var film = await _world.AddFilmAsync(headCoach);
            int videoId = await _world.AddVideoWithFileAsync(headCoach, film.GameId);
            int clipId = await _world.AddClipAsync(headCoach, videoId, 10, 18);

            int draft = await _world.AddCutupAsync(headCoach, "Still a draft", clipId);
            int made = await _world.AddCutupAsync(headCoach, "Already a video", clipId);

            using var context = _world.NewContext();
            var clips = _world.Clips(context);
            var cutups = _world.Cutups(context);

            await cutups.SetSharingAsync(headCoach, draft, new SetCutupSharingDto { WholeTeam = true });
            await cutups.SetSharingAsync(headCoach, made, new SetCutupSharingDto { WholeTeam = true });
            await cutups.RequestRenderAsync(headCoach, made);
            await _world.Renderer(context).RenderNextAsync(CancellationToken.None);

            // Drawn after the second cutup was made into a video
            await clips.SaveAnnotationsAsync(headCoach, clipId, new SaveAnnotationsDto { AnnotationsJson = Drawings() });

            // The draft follows its clip; the film that was already made stays as it was shared
            var draftPlay = (await cutups.GetCutupAsync(player, draft))!.Plays.Single();
            Assert.Equal(12_000, Read(draftPlay.AnnotationsJson).Stops.Single().AtMs);
            Assert.Equal(10_000, draftPlay.StartMs);   // what a stop's time is measured from

            Assert.Null((await cutups.GetCutupAsync(player, made))!.Plays.Single().AnnotationsJson);
        }

        [Fact]
        public async Task OnlyCoachesOfTheTeam_DrawOnAPlay()
        {
            var headCoach = await _world.AddTeamAsync();
            var player = await _world.AddPlayerAsync(headCoach, "Walker", "Receivers");
            var film = await _world.AddFilmAsync(headCoach);
            int clipId = await _world.AddClipAsync(headCoach, film.VideoId, 10, 18);
            var outsider = await _world.AddTeamAsync("Other Team");

            using var context = _world.NewContext();
            var clips = _world.Clips(context);
            var drawings = new SaveAnnotationsDto { AnnotationsJson = Drawings() };

            await Assert.ThrowsAsync<ForbiddenException>(() => clips.SaveAnnotationsAsync(player, clipId, drawings));
            Assert.False(await clips.SaveAnnotationsAsync(outsider, clipId, drawings));

            Assert.Null(await context.Clips.Where(c => c.Id == clipId).Select(c => c.AnnotationsJson).SingleAsync());
        }
    }
}
