using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Run and pass tendencies, counted from the plays the coaches have tagged.
    // The API does the counting; this controller turns the page's filters into its query and back.
    public class TendenciesController : WarRoomController
    {
        private readonly HttpClient _client;

        public TendenciesController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Tendencies?teamId=2&gameIds=2&gameIds=3&down=1&distance=Short   (every filter is optional)
        public async Task<IActionResult> Index(int? teamId, List<int> gameIds, int? down, DistanceRange? distance)
        {
            // The page is for coaches. The API refuses players too.
            if (User.IsPlayer())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var teamsResponse = await _client.GetAsync("api/ScoutedTeam");
            if (!teamsResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(teamsResponse);
            }

            var model = new TendenciesViewModel
            {
                Teams = (await teamsResponse.Content.ReadFromJsonAsync<List<ScoutedTeamDto>>())!,
                Down = down >= 1 && down <= 4 ? down : null,
                Distance = distance
            };

            // The first team in the list (the team itself) is shown until another one is chosen
            model.Team = model.Teams.FirstOrDefault(t => t.Id == teamId) ?? model.Teams.FirstOrDefault();
            if (model.Team == null)
            {
                return View(model);   // Index View with nothing to show yet
            }

            var gamesResponse = await _client.GetAsync($"api/Game?scoutedTeamId={model.Team.Id}");
            if (!gamesResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(gamesResponse);
            }
            model.Games = (await gamesResponse.Content.ReadFromJsonAsync<List<GameDto>>())!;

            // A down the team's game does not have is not a choice: a 4th down in an address
            // that was kept from before the team took up Canadian football
            if (model.Down > (model.Games.FirstOrDefault()?.Downs ?? 4))
            {
                model.Down = null;
            }

            // Only this team's games can be chosen. Choosing none of them, or all of them, counts every game.
            model.GameIds = gameIds.Where(id => model.Games.Any(g => g.Id == id)).Distinct().ToList();
            if (model.GameIds.Count == model.Games.Count)
            {
                model.GameIds.Clear();
            }

            var reportResponse = await _client.GetAsync("api/Tendency?" + ApiQuery(model.Team.Id, model.GameIds, model.Down, model.Distance));
            if (!reportResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(reportResponse);
            }
            model.Report = await reportResponse.Content.ReadFromJsonAsync<TendencyReportDto>();

            return View(model);   // Index View
        }

        // GET: Tendencies/Plays?teamId=2&gameIds=2&down=3&distance=Long   (the plays behind one figure)
        public async Task<IActionResult> Plays(int? teamId, List<int> gameIds, int? down, DistanceRange? distance, bool leftOut = false)
        {
            if (User.IsPlayer())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var teamsResponse = await _client.GetAsync("api/ScoutedTeam");
            if (!teamsResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(teamsResponse);
            }
            var teams = (await teamsResponse.Content.ReadFromJsonAsync<List<ScoutedTeamDto>>())!;

            var model = new TendencyPlaysViewModel
            {
                Team = teams.FirstOrDefault(t => t.Id == teamId),
                Down = !leftOut && down >= 1 && down <= 4 ? down : null,
                Distance = leftOut ? null : distance,
                LeftOut = leftOut
            };
            gameIds = gameIds.Distinct().ToList();

            // The same filters the figure was counted with, and only the plays the charts count.
            // "leftOut" asks for the other ones: tagged with a down the team's game does not have.
            string charted = leftOut ? "false" : "true";
            var clipsResponse = await _client.GetAsync($"api/Clip?charted={charted}&" + ApiQuery(model.Team?.Id, gameIds, model.Down, model.Distance));
            if (!clipsResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(clipsResponse);
            }
            model.Clips = (await clipsResponse.Content.ReadFromJsonAsync<List<ClipDto>>())!;
            model.GameCount = model.Clips.Select(c => c.GameId).Distinct().Count();

            // The way back keeps the team and games; the situation is kept too, so the same figure is shown again
            var back = new TendenciesViewModel { Team = model.Team, GameIds = gameIds };
            model.BackQuery = back.Query(model.Down, model.Distance);

            return View(model);   // Plays View
        }

        // The filters as the API takes them, both for the tendency figures and for the list of clips
        private static string ApiQuery(int? offenceTeamId, List<int> gameIds, int? down, DistanceRange? distance)
        {
            var parts = new List<string>();

            if (offenceTeamId != null)
            {
                parts.Add($"offenceTeamId={offenceTeamId}");
            }
            foreach (int gameId in gameIds)
            {
                parts.Add($"gameIds={gameId}");
            }
            if (down != null)
            {
                parts.Add($"down={down}");
            }
            if (distance != null)
            {
                parts.Add($"distance={distance}");
            }

            return string.Join("&", parts);
        }
    }
}
