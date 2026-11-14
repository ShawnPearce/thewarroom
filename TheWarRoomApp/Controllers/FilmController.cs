using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Game film: the games the team has film of, their videos, and the plays cut out of them and tagged.
    // This is the coaches' working material. Players watch the cutups shared with them (see CutupsController).
    public class FilmController : WarRoomController
    {
        private readonly HttpClient _client;

        public FilmController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // ---------- Games ----------

        // GET: Film?teamId=3
        public async Task<IActionResult> Index(int? teamId = null)
        {
            // A player's film is the cutups shared with them
            if (User.IsPlayer())
            {
                return RedirectToAction("Index", "Cutups");
            }

            string url = teamId != null ? $"api/Game?scoutedTeamId={teamId}" : "api/Game";
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var teamsResponse = await _client.GetAsync("api/ScoutedTeam");
            if (!teamsResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(teamsResponse);
            }

            return View(new FilmIndexViewModel
            {
                Games = (await response.Content.ReadFromJsonAsync<List<GameDto>>())!,
                Teams = (await teamsResponse.Content.ReadFromJsonAsync<List<ScoutedTeamDto>>())!,
                TeamId = teamId
            });   // Index View
        }

        // GET: Film/Game/5   (a game with its videos)
        public async Task<IActionResult> Game(int id)
        {
            var response = await _client.GetAsync($"api/Game/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var game = await response.Content.ReadFromJsonAsync<GameDetailDto>();
            return View(game);   // Game View
        }

        // GET: Film/CreateGame
        public async Task<IActionResult> CreateGame()
        {
            var teams = await GetTeamsAsync();
            if (teams == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // Most games on film are the team's own, so the team itself starts as the home team
            return View(new GameFormViewModel
            {
                Game = new SaveGameDto
                {
                    GameDate = DateOnly.FromDateTime(DateTime.Today),
                    HomeTeamId = teams.FirstOrDefault(t => t.IsOwnTeam)?.Id ?? 0
                },
                Teams = teams
            });
        }

        // POST: Film/CreateGame
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGame(GameFormViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/Game", model.Game);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var created = await response.Content.ReadFromJsonAsync<GameDto>();
                    return RedirectToAction(nameof(Game), new { id = created!.Id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 for example the same team twice
                {
                    ModelState.AddModelError("Game.AwayTeamId", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            model.Teams = await GetTeamsAsync() ?? new List<ScoutedTeamDto>();
            return View(model);   // CreateGame View with errors
        }

        // GET: Film/EditGame/5
        public async Task<IActionResult> EditGame(int id)
        {
            var response = await _client.GetAsync($"api/Game/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var game = (await response.Content.ReadFromJsonAsync<GameDetailDto>())!;

            return View(new GameFormViewModel
            {
                Id = game.Id,
                Game = new SaveGameDto
                {
                    GameDate = game.GameDate,
                    HomeTeamId = game.HomeTeamId,
                    AwayTeamId = game.AwayTeamId,
                    Label = game.Label,
                    Notes = game.Notes
                },
                Teams = await GetTeamsAsync() ?? new List<ScoutedTeamDto>()
            });
        }

        // POST: Film/EditGame/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGame(int id, GameFormViewModel model)
        {
            if (id != model.Id)   // id in URL must match the form
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/Game/{id}", model.Game);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Game), new { id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 for example a team that tagged plays still need
                {
                    ModelState.AddModelError("Game.AwayTeamId", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            model.Teams = await GetTeamsAsync() ?? new List<ScoutedTeamDto>();
            return View(model);   // EditGame View with errors
        }

        // GET: Film/DeleteGame/5
        public async Task<IActionResult> DeleteGame(int id)
        {
            var response = await _client.GetAsync($"api/Game/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var game = await response.Content.ReadFromJsonAsync<GameDetailDto>();
            return View(game);   // Delete confirmation View
        }

        // POST: Film/DeleteGame/5
        [HttpPost, ActionName("DeleteGame")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGameConfirmed(int id)
        {
            var response = await _client.DeleteAsync($"api/Game/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The game was deleted.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 the game has video
            {
                return RedirectToAction(nameof(Game), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- A video and its plays ----------

        // GET: Film/Video/5   (a video with its plays in the order they happen)
        public async Task<IActionResult> Video(int id)
        {
            var response = await _client.GetAsync($"api/Video/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var video = await response.Content.ReadFromJsonAsync<VideoDetailDto>();
            return View(video);   // Video View
        }

        // GET: Film/CreateClip?videoId=5
        public async Task<IActionResult> CreateClip(int videoId)
        {
            var response = await _client.GetAsync($"api/Video/{videoId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            return View("ClipForm", new ClipFormViewModel
            {
                VideoId = videoId,
                Video = (await response.Content.ReadFromJsonAsync<VideoDetailDto>())!
            });
        }

        // POST: Film/CreateClip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClip(ClipFormViewModel model)
        {
            var clip = ToSaveClipDto(model);
            if (clip != null)   // the form is complete and its times could be read
            {
                var response = await _client.PostAsJsonAsync("api/Clip", clip);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    TempData["Message"] = "The play was added.";
                    return RedirectToAction(nameof(Video), new { id = model.VideoId });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 for example it ends after the end of the video
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return await ClipFormWithErrorsAsync(model);
        }

        // GET: Film/EditClip/40
        public async Task<IActionResult> EditClip(int id)
        {
            var response = await _client.GetAsync($"api/Clip/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }
            var clip = (await response.Content.ReadFromJsonAsync<ClipDto>())!;

            var videoResponse = await _client.GetAsync($"api/Video/{clip.VideoId}");
            if (!videoResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(videoResponse);
            }

            return View("ClipForm", new ClipFormViewModel
            {
                Id = clip.Id,
                VideoId = clip.VideoId,
                Start = TimeText.Format(clip.StartMs),
                End = TimeText.Format(clip.EndMs),
                Title = clip.Title,
                PlayType = clip.Tag?.PlayType,
                OffenceTeamId = clip.Tag?.OffenceTeamId,
                Down = clip.Tag?.Down,
                Distance = clip.Tag?.Distance,
                Video = (await videoResponse.Content.ReadFromJsonAsync<VideoDetailDto>())!
            });
        }

        // POST: Film/EditClip/40
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClip(int id, ClipFormViewModel model)
        {
            if (id != model.Id)   // id in URL must match the form
            {
                return BadRequest();
            }

            var clip = ToSaveClipDto(model);
            if (clip != null)
            {
                var response = await _client.PutAsJsonAsync($"api/Clip/{id}", clip);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    TempData["Message"] = "The play was saved.";
                    return RedirectToAction(nameof(Video), new { id = model.VideoId });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return await ClipFormWithErrorsAsync(model);
        }

        // POST: Film/DeleteClip/40
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClip(int id, int videoId)
        {
            var response = await _client.DeleteAsync($"api/Clip/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The play was deleted.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 the play is used in a cutup
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Video), new { id = videoId });
        }

        // ---------- Helpers ----------

        // The teams on film, for the drop-downs of the game form. Null when the API refuses (players).
        private async Task<List<ScoutedTeamDto>?> GetTeamsAsync()
        {
            var response = await _client.GetAsync("api/ScoutedTeam");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<ScoutedTeamDto>>();
        }

        // Turns the play form into what the API takes: times in milliseconds, and the tag as one optional part.
        // Returns null, with the reasons added to ModelState, when the form cannot be sent as it is.
        private SaveClipDto? ToSaveClipDto(ClipFormViewModel model)
        {
            int startMs = 0;
            int endMs = 0;
            const string timeHint = "Enter a time like 12:34 or 1:02:03.";

            if (!string.IsNullOrWhiteSpace(model.Start) && !TimeText.TryParse(model.Start, out startMs))
            {
                ModelState.AddModelError(nameof(model.Start), timeHint);
            }
            if (!string.IsNullOrWhiteSpace(model.End) && !TimeText.TryParse(model.End, out endMs))
            {
                ModelState.AddModelError(nameof(model.End), timeHint);
            }

            // A tagged play needs all of its tag; an untagged one needs none of it
            bool tagged = model.PlayType != null;
            if (tagged)
            {
                if (model.OffenceTeamId == null)
                {
                    ModelState.AddModelError(nameof(model.OffenceTeamId), "Choose the team on offence.");
                }
                if (model.Down == null)
                {
                    ModelState.AddModelError(nameof(model.Down), "Choose the down.");
                }
                if (model.Distance == null)
                {
                    ModelState.AddModelError(nameof(model.Distance), "Enter the yards to go.");
                }
            }
            else if (model.OffenceTeamId != null || model.Down != null || model.Distance != null)
            {
                ModelState.AddModelError(nameof(model.PlayType), "Choose run or pass to tag the play, or clear the rest of the tag.");
            }

            if (!ModelState.IsValid)
            {
                return null;
            }

            return new SaveClipDto
            {
                VideoId = model.VideoId,
                StartMs = startMs,
                EndMs = endMs,
                Title = model.Title,
                Tag = tagged
                    ? new SavePlayTagDto
                    {
                        PlayType = model.PlayType!.Value,
                        OffenceTeamId = model.OffenceTeamId!.Value,
                        Down = model.Down!.Value,
                        Distance = model.Distance!.Value
                    }
                    : null
            };
        }

        // ClipForm View with errors: what was typed comes back from the form, the video is read again
        private async Task<IActionResult> ClipFormWithErrorsAsync(ClipFormViewModel model)
        {
            var response = await _client.GetAsync($"api/Video/{model.VideoId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            model.Video = (await response.Content.ReadFromJsonAsync<VideoDetailDto>())!;
            return View("ClipForm", model);
        }
    }
}
