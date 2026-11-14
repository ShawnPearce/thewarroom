using TheWarRoomAPI.Diagrams;

namespace TheWarRoomAPI.Tests
{
    // A diagram comes from the browser as text, so it is checked like any other input before it is stored
    public class DiagramValidatorTests
    {
        private static DiagramDocument SimpleDiagram()
        {
            return new DiagramDocument
            {
                Unit = "Offence",
                Players =
                {
                    new DiagramPlayer { Id = "qb", Label = "QB", X = 0, Y = -5 },
                    new DiagramPlayer
                    {
                        Id = "x", Label = "X", X = -22, Y = -1,
                        Paths = { new DiagramPath { Kind = "route", Pts = { new[] { 0.0, 6.0 }, new[] { 5.0, 10.0 } } } }
                    }
                },
                Drawings = { new DiagramDrawing { Id = "t1", Type = "text", X = -20, Y = 15, Text = "Read the corner" } }
            };
        }

        private static string Json(DiagramDocument diagram)
        {
            return DiagramValidator.Serialize(diagram);
        }

        [Fact]
        public void APlay_KeepsItsPlayersRoutesAndText()
        {
            string stored = DiagramValidator.Normalize(Json(SimpleDiagram()), "Offence", isFormation: false);

            var diagram = DiagramValidator.Parse(stored);
            Assert.Equal(2, diagram.Players.Count);
            Assert.Equal(2, diagram.Players.Single(p => p.Id == "x").Paths.Single().Pts.Count);
            Assert.Equal("Read the corner", diagram.Drawings.Single().Text);
        }

        [Fact]
        public void AFormation_KeepsOnlyThePlayers()
        {
            string stored = DiagramValidator.Normalize(Json(SimpleDiagram()), "Offence", isFormation: true);

            var diagram = DiagramValidator.Parse(stored);
            Assert.Equal(2, diagram.Players.Count);
            Assert.All(diagram.Players, p => Assert.Empty(p.Paths));
            Assert.Empty(diagram.Drawings);
        }

        [Fact]
        public void TheUnitComesFromThePlaybook_NotFromTheBrowser()
        {
            var sent = SimpleDiagram();
            sent.Unit = "Defence";

            string stored = DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false);

            Assert.Equal("Offence", DiagramValidator.Parse(stored).Unit);
        }

        [Fact]
        public void AnythingThatIsNotPartOfTheFormat_IsDropped_AndNumbersAreRounded()
        {
            const string sent = """
                {"schemaVersion":1,"unit":"Offence","view":{"yMin":-12,"yMax":18},"ballX":0,
                 "somethingElse":"<script>alert(1)</script>",
                 "players":[{"id":"qb","team":"own","label":" QB ","shape":"circle","x":1.23456,"y":-5,"paths":[],"onclick":"x"}],
                 "drawings":[]}
                """;

            string stored = DiagramValidator.Normalize(sent, "Offence", isFormation: false);

            Assert.DoesNotContain("somethingElse", stored);
            Assert.DoesNotContain("onclick", stored);
            Assert.DoesNotContain("script", stored);

            var player = DiagramValidator.Parse(stored).Players.Single();
            Assert.Equal(1.23, player.X);
            Assert.Equal("QB", player.Label);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("{\"schemaVersion\":1,\"players\":\"oops\"}")]
        public void TextThatIsNotADiagram_IsRejected(string sent)
        {
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(sent, "Offence", isFormation: false));
        }

        [Fact]
        public void ADiagramFromAnotherVersionOfTheDesigner_IsRejected()
        {
            var sent = SimpleDiagram();
            sent.SchemaVersion = 2;

            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false));
        }

        [Fact]
        public void ADiagramWithoutOwnPlayers_IsRejected()
        {
            var sent = SimpleDiagram();
            foreach (var player in sent.Players)
            {
                player.Team = "opp";
            }

            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false));
        }

        [Fact]
        public void MoreThanElevenPlayersOnASide_IsRejected()
        {
            var sent = new DiagramDocument { Unit = "Offence" };
            for (int i = 0; i < 12; i++)
            {
                sent.Players.Add(new DiagramPlayer { Id = $"p{i}", Label = "P", X = i - 6, Y = -1 });
            }

            var error = Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: true));
            Assert.Contains("11 players", error.Message);
        }

        [Fact]
        public void ElevenOfOurPlayersPlusElevenOpponents_IsAllowed()
        {
            var sent = new DiagramDocument { Unit = "Offence" };
            for (int i = 0; i < 11; i++)
            {
                sent.Players.Add(new DiagramPlayer { Id = $"o{i}", Team = "own", Label = "O", X = i - 5, Y = -1 });
                sent.Players.Add(new DiagramPlayer { Id = $"d{i}", Team = "opp", Label = "D", X = i - 5, Y = 2 });
            }

            string stored = DiagramValidator.Normalize(Json(sent), "Offence", isFormation: true);

            Assert.Equal(22, DiagramValidator.Parse(stored).Players.Count);
        }

        [Fact]
        public void TwoPlayersWithTheSameId_AreRejected()
        {
            var sent = SimpleDiagram();
            sent.Players[1].Id = "qb";

            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false));
        }

        [Theory]
        [InlineData(30, -1)]     // past the sideline
        [InlineData(0, -20)]     // below the part of the field that is shown
        [InlineData(0, 25)]      // above it
        public void APlayerOffTheField_IsRejected(double x, double y)
        {
            var sent = SimpleDiagram();
            sent.Players[0].X = x;
            sent.Players[0].Y = y;

            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false));
        }

        [Fact]
        public void ARouteThatLeavesTheField_IsRejected()
        {
            var sent = SimpleDiagram();
            sent.Players[1].Paths[0].Pts.Add(new[] { 0.0, 40.0 });   // 39 yards downfield, the view stops at 18

            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(sent), "Offence", isFormation: false));
        }

        [Fact]
        public void TheLimitsOnLabelsRoutesAndText_AreEnforced()
        {
            var longLabel = SimpleDiagram();
            longLabel.Players[0].Label = "QUARTERBACK";
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(longLabel), "Offence", isFormation: false));

            var tooManyPaths = SimpleDiagram();
            for (int i = 0; i < 3; i++)
            {
                tooManyPaths.Players[1].Paths.Add(new DiagramPath { Kind = "route", Pts = { new[] { 1.0, 1.0 } } });
            }
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(tooManyPaths), "Offence", isFormation: false));

            var unknownKind = SimpleDiagram();
            unknownKind.Players[1].Paths[0].Kind = "teleport";
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(unknownKind), "Offence", isFormation: false));

            var longText = SimpleDiagram();
            longText.Drawings[0].Text = new string('a', 61);
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(Json(longText), "Offence", isFormation: false));

            var huge = "{\"schemaVersion\":1,\"padding\":\"" + new string('x', 70_000) + "\"}";
            Assert.Throws<InvalidOperationException>(() => DiagramValidator.Normalize(huge, "Offence", isFormation: false));
        }

        [Fact]
        public void EveryBaseFormation_IsAValidDiagramWithElevenPlayers()
        {
            foreach (var template in TheWarRoomAPI.Data.FormationTemplates.All())
            {
                string stored = DiagramValidator.Normalize(Json(template.Diagram), template.Unit.ToString(), isFormation: true);
                Assert.Equal(11, DiagramValidator.Parse(stored).Players.Count);
            }
        }
    }
}
