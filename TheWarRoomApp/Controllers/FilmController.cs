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

        // Video files go through a client of their own (see Program.cs)
        private readonly HttpClient _files;

        public FilmController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
            _files = httpClientFactory.CreateClient("WarRoomAPIFiles");
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

            var setup = await GetSetupAsync();
            if (setup == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(new FilmGameViewModel
            {
                Game = (await response.Content.ReadFromJsonAsync<GameDetailDto>())!,
                Setup = setup
            });   // Game View
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

        // ---------- Uploading a video ----------

        // GET: Film/Upload?gameId=5
        public async Task<IActionResult> Upload(int gameId)
        {
            var response = await _client.GetAsync($"api/Game/{gameId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var setup = await GetSetupAsync();
            if (setup == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var game = (await response.Content.ReadFromJsonAsync<GameDetailDto>())!;

            return View(new FilmGameViewModel { Game = game, Setup = setup });   // Upload View
        }

        // The three actions below are called by the upload page's script (video-upload.js), one after another.
        // They answer with a status code and a short text instead of a page.

        // POST: Film/StartUpload   (tells the API about the file; the answer says where the browser sends it)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartUpload([FromForm] StartUploadDto upload)
        {
            if (!ModelState.IsValid)   // check for validation errors
            {
                return StatusCode(StatusCodes.Status400BadRequest, FirstError());
            }

            var response = await _client.PostAsJsonAsync("api/Video", upload);
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);   // 409 not a video file, too large, or no storage
            }

            var ticket = (await response.Content.ReadFromJsonAsync<UploadTicketDto>())!;   // 201 Created

            // With a signed link the file goes straight to film storage. Without one it is sent to this site.
            return Json(new
            {
                videoId = ticket.VideoId,
                url = ticket.UploadUrl ?? Url.Action(nameof(UploadFile), new { id = ticket.VideoId }),
                throughSite = ticket.UploadUrl == null,
                contentType = ticket.ContentType
            });
        }

        // PUT: Film/UploadFile/5   (the file itself, when film storage has no signed links)
        [HttpPut]
        [ValidateAntiForgeryToken]
        [DisableRequestSizeLimit]   // the API stops reading at the largest size that is taken
        public async Task<IActionResult> UploadFile(int id)
        {
            // The file is passed on to the API as it arrives, without being held in memory or on disk here
            var file = new StreamContent(Request.Body);
            file.Headers.ContentLength = Request.ContentLength;

            using var response = await _files.PutAsync($"api/Video/{id}/file", file, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);
            }

            return NoContent();   // 204 No Content
        }

        // POST: Film/FinishUpload/5   (the API checks the file that arrived)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishUpload(int id)
        {
            var response = await _client.PostAsync($"api/Video/{id}/complete", null);
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);   // 409 with the reason the file cannot be used
            }

            TempData["Message"] = "The video was uploaded and checked. It can be cut into plays now.";
            return Json(new { url = Url.Action(nameof(Video), new { id }) });
        }

        // POST: Film/CancelUpload/5   (an upload that was stopped leaves no record behind)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelUpload(int id)
        {
            var response = await _client.GetAsync($"api/Video/{id}");
            if (response.IsSuccessStatusCode)
            {
                // Only a video that never became ready is removed this way
                var video = (await response.Content.ReadFromJsonAsync<VideoDetailDto>())!;
                if (video.Status != VideoStatus.Ready)
                {
                    await _client.DeleteAsync($"api/Video/{id}");
                }
            }

            return NoContent();   // 204 No Content
        }

        // GET: Film/Stream/5   (the video itself, for the player on the video's page)
        public async Task<IActionResult> Stream(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/Video/{id}/file");

            // A player asks for the part of the video it needs next
            if (Request.Headers.TryGetValue("Range", out var range))
            {
                request.Headers.TryAddWithoutValidation("Range", range.ToString());
            }

            var response = await _files.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Found)
            {
                return new RelayedFileResult(response);   // 200, 206 Partial Content, or 302 to a signed link
            }

            using (response)
            {
                return StatusCode((int)response.StatusCode);   // a player has no use for a page: 401, 403, 404 or 416 as it is
            }
        }

        // GET: Film/DeleteVideo/5
        public async Task<IActionResult> DeleteVideo(int id)
        {
            var response = await _client.GetAsync($"api/Video/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var video = await response.Content.ReadFromJsonAsync<VideoDetailDto>();
            return View(video);   // Delete confirmation View
        }

        // POST: Film/DeleteVideo/5
        [HttpPost, ActionName("DeleteVideo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVideoConfirmed(int id, int gameId)
        {
            var response = await _client.DeleteAsync($"api/Video/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The video was deleted.";
                return RedirectToAction(nameof(Game), new { id = gameId });
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 its plays are used in a cutup
            {
                return RedirectToAction(nameof(Video), new { id });
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

        // The two actions below are called by the editor on a video's page (clip-editor.js).
        // They answer with the list of plays as the server draws it, or with a status code and a short text.

        // POST: Film/SavePlay   (a new play, or a change to one, marked on the video itself)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePlay([FromForm] PlayEditModel model)
        {
            if (!ModelState.IsValid)   // check for validation errors
            {
                return StatusCode(StatusCodes.Status400BadRequest, FirstError());
            }

            // A tagged play needs all of its tag; an untagged one needs none of it
            bool tagged = model.PlayType != null;
            if (tagged && (model.OffenceTeamId == null || model.Down == null || model.Distance == null))
            {
                return StatusCode(StatusCodes.Status400BadRequest, "A tagged play needs the team on offence, the down and the yards to go.");
            }
            if (!tagged && (model.Down != null || model.Distance != null))
            {
                return StatusCode(StatusCodes.Status400BadRequest, "Choose run or pass to tag the play, or clear the down and the yards to go.");
            }

            var clip = new SaveClipDto
            {
                VideoId = model.VideoId,
                StartMs = model.StartMs,
                EndMs = model.EndMs,
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

            var response = model.Id == 0
                ? await _client.PostAsJsonAsync("api/Clip", clip)            // 201 Created
                : await _client.PutAsJsonAsync($"api/Clip/{model.Id}", clip);   // 204 No Content
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);   // 409 for example it ends after the end of the video
            }

            return await PlayListAsync(model.VideoId);
        }

        // POST: Film/DeletePlay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlay([FromForm] int id, [FromForm] int videoId)
        {
            var response = await _client.DeleteAsync($"api/Clip/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);   // 409 the play is used in a cutup
            }

            return await PlayListAsync(videoId);
        }

        // POST: Film/SaveDrawings   (the arrows and text drawn on a play; sending none takes them away)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDrawings([FromForm] int id, [FromForm] int videoId, [FromForm] string? annotationsJson)
        {
            var response = await _client.PutAsJsonAsync($"api/Clip/{id}/annotations", new SaveAnnotationsDto { AnnotationsJson = annotationsJson });
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);   // 409 for example a drawing outside the play
            }

            return await PlayListAsync(videoId);
        }

        // ---------- Helpers ----------

        // Whether this server can take video. Null when the API refuses (players).
        private async Task<FilmSetupDto?> GetSetupAsync()
        {
            var response = await _client.GetAsync("api/FilmSetup");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<FilmSetupDto>();
        }

        // For actions that are called by a script: the API's status code and its message, as they are.
        // The script shows the message; a page would be of no use to it.
        private async Task<IActionResult> ScriptFailureAsync(HttpResponseMessage response)
        {
            string message = await response.Content.ReadAsStringAsync();

            return StatusCode((int)response.StatusCode, response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.Forbidden
                ? message
                : "That could not be done. Reload the page and try again.");
        }

        // The first validation message of what a script sent
        private string FirstError()
        {
            return ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrEmpty(m))
                ?? "What was sent could not be read.";
        }

        // The list of a video's plays, drawn by the server, for the editor to put on the page
        private async Task<IActionResult> PlayListAsync(int videoId)
        {
            var response = await _client.GetAsync($"api/Video/{videoId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ScriptFailureAsync(response);
            }

            var video = (await response.Content.ReadFromJsonAsync<VideoDetailDto>())!;
            return PartialView("_PlayList", new PlayListViewModel { Video = video, OnTheVideo = video.HasFile });
        }

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
