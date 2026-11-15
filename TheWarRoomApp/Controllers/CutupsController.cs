using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Cutups: edited film made of chosen plays in a chosen order.
    // Coaches build and share them; a player sees the ones shared with them, their groups or the whole team.
    public class CutupsController : WarRoomController
    {
        private readonly HttpClient _client;

        // Video files go through a client of their own (see Program.cs)
        private readonly HttpClient _files;

        public CutupsController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
            _files = httpClientFactory.CreateClient("WarRoomAPIFiles");
        }

        // GET: Cutups   (a coach's list of all cutups, or a player's "my film")
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/Cutup");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var cutups = await response.Content.ReadFromJsonAsync<List<CutupDto>>();
            return View(cutups);   // Index View
        }

        // GET: Cutups/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 403 when it has not been shared with this player
            }

            var cutup = await response.Content.ReadFromJsonAsync<CutupDetailDto>();
            return View(cutup);   // Details View
        }

        // GET: Cutups/Create
        public IActionResult Create()
        {
            // The page is for coaches. The API enforces this too when the form is saved.
            if (User.IsPlayer())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(new SaveCutupDto { ForWeekOf = DateOnly.FromDateTime(DateTime.Today) });
        }

        // POST: Cutups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaveCutupDto cutup)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/Cutup", cutup);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    // A new cutup has no plays yet, so the next page is where they are chosen
                    var created = await response.Content.ReadFromJsonAsync<CutupDto>();
                    return RedirectToAction(nameof(AddPlays), new { id = created!.Id });
                }

                return await ApiFailureAsync(response);
            }

            return View(cutup);   // Create View with errors
        }

        // GET: Cutups/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var cutup = (await response.Content.ReadFromJsonAsync<CutupDetailDto>())!;
            if (!cutup.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewData["CutupId"] = id;
            return View(new SaveCutupDto
            {
                Name = cutup.Name,
                Description = cutup.Description,
                ForWeekOf = cutup.ForWeekOf
            });
        }

        // POST: Cutups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SaveCutupDto cutup)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/Cutup/{id}", cutup);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Details), new { id });
                }

                return await ApiFailureAsync(response);
            }

            ViewData["CutupId"] = id;
            return View(cutup);   // Edit View with errors
        }

        // GET: Cutups/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var cutup = (await response.Content.ReadFromJsonAsync<CutupDetailDto>())!;
            if (!cutup.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(cutup);   // Delete confirmation View
        }

        // POST: Cutups/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var response = await _client.DeleteAsync($"api/Cutup/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The cutup was deleted. The plays it used are still on film.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 it is being made into a video right now
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- The plays of a cutup ----------

        // GET: Cutups/AddPlays/5?gameId=2&offenceTeamId=3&playType=Run&down=1
        public async Task<IActionResult> AddPlays(int id, int? gameId, int? offenceTeamId, PlayType? playType, int? down)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var cutup = (await response.Content.ReadFromJsonAsync<CutupDetailDto>())!;
            if (!cutup.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var gamesResponse = await _client.GetAsync("api/Game");
            if (!gamesResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(gamesResponse);
            }

            var model = new CutupAddPlaysViewModel
            {
                Cutup = cutup,
                Games = (await gamesResponse.Content.ReadFromJsonAsync<List<GameDto>>())!.Where(g => g.ClipCount > 0).ToList(),
                OffenceTeamId = offenceTeamId,
                PlayType = playType,
                Down = down
            };

            // The plays of one game are shown at a time: the chosen game, or the most recent one
            model.Game = model.Games.FirstOrDefault(g => g.Id == gameId) ?? model.Games.FirstOrDefault();
            if (model.Game != null)
            {
                // A team that did not play in this game cannot be on offence in it
                if (offenceTeamId != model.Game.HomeTeamId && offenceTeamId != model.Game.AwayTeamId)
                {
                    model.OffenceTeamId = null;
                }

                string url = $"api/Clip?gameId={model.Game.Id}";
                if (model.OffenceTeamId != null)
                {
                    url += $"&offenceTeamId={model.OffenceTeamId}";
                }
                if (playType != null)
                {
                    url += $"&playType={playType}";
                }
                if (down != null)
                {
                    url += $"&down={down}";
                }

                var clipsResponse = await _client.GetAsync(url);
                if (!clipsResponse.IsSuccessStatusCode)
                {
                    return await ApiFailureAsync(clipsResponse);
                }

                model.Clips = (await clipsResponse.Content.ReadFromJsonAsync<List<ClipDto>>())!;
            }

            return View(model);   // AddPlays View
        }

        // POST: Cutups/AddPlays/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlays(int id, int? gameId, List<int> clipIds)
        {
            if (clipIds.Count == 0)
            {
                TempData["Error"] = "Tick the plays to add first.";
                return RedirectToAction(nameof(AddPlays), new { id, gameId });
            }

            var response = await _client.PostAsJsonAsync($"api/Cutup/{id}/clips", new AddCutupClipsDto { ClipIds = clipIds });
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = clipIds.Count == 1 ? "The play was added to the cutup." : $"{clipIds.Count} plays were added to the cutup.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 the cutup is no longer a draft
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Cutups/Move   (one step earlier or later)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Move(int id, int playId, MoveDirection direction)
        {
            var response = await _client.PostAsJsonAsync($"api/Cutup/plays/{playId}/move", new MoveCutupPlayDto { Direction = direction });
            if (!response.IsSuccessStatusCode && !await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Cutups/Remove   (takes the play out of the cutup; the clip stays on film)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, int playId)
        {
            var response = await _client.DeleteAsync($"api/Cutup/plays/{playId}");
            if (!response.IsSuccessStatusCode && !await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- The video of a cutup ----------

        // POST: Cutups/Render/5   (asks for the cutup to be made into one video; its plays are fixed from then on)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Render(int id)
        {
            var response = await _client.PostAsync($"api/Cutup/{id}/render", null);
            if (response.IsSuccessStatusCode)   // 202 Accepted: it is in the queue
            {
                TempData["Message"] = "The video is being made. This page shows how far it has come.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 for example a play from a video without a file
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Cutups/BackToDraft/5   (the plays can be changed again; a video that was made is thrown away)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackToDraft(int id)
        {
            var response = await _client.PostAsync($"api/Cutup/{id}/draft", null);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The cutup is a draft again. Change its plays, then make the video again.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 it is being made into a video right now
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Cutups/Status/5   (asked every few seconds by the page of a cutup that is being made into a video)
        public async Task<IActionResult> Status(int id)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);   // the script only needs to know it should stop asking
            }

            var cutup = (await response.Content.ReadFromJsonAsync<CutupDetailDto>())!;
            return Json(new { status = cutup.Status.ToString(), progressPercent = cutup.ProgressPercent });
        }

        // GET: Cutups/Stream/5   (the finished video, for the player on the cutup's page)
        public async Task<IActionResult> Stream(int id)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/Cutup/{id}/file");

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
                return StatusCode((int)response.StatusCode);   // 403 when the film has not been shared with this player
            }
        }

        // ---------- Sharing ----------

        // GET: Cutups/Sharing/5
        public async Task<IActionResult> Sharing(int id)
        {
            var response = await _client.GetAsync($"api/Cutup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var cutup = (await response.Content.ReadFromJsonAsync<CutupDetailDto>())!;
            if (!cutup.CanManage || cutup.Sharing == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var groupsResponse = await _client.GetAsync("api/PositionGroup");
            if (!groupsResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(groupsResponse);
            }
            var groups = (await groupsResponse.Content.ReadFromJsonAsync<List<PositionGroupDto>>())!;

            var rosterResponse = await _client.GetAsync("api/Roster");
            if (!rosterResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(rosterResponse);
            }
            var roster = (await rosterResponse.Content.ReadFromJsonAsync<List<RosterEntryDto>>())!;

            return View(new CutupSharingViewModel
            {
                CutupId = cutup.Id,
                CutupName = cutup.Name,
                WholeTeam = cutup.Sharing.WholeTeam,
                Groups = groups
                    .Select(g => new MemberChoice
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Detail = DisplayText.Unit(g.Unit),
                        IsSelected = cutup.Sharing.Groups.Any(s => s.Id == g.Id)
                    })
                    .ToList(),
                Players = roster
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.JerseyNumber ?? 999)
                    .Select(p => new MemberChoice
                    {
                        Id = p.PlayerId,
                        Name = DisplayText.Player(p.JerseyNumber, p.FirstName, p.LastName),
                        // The player's groups, so a coach can see who is already reached through a group
                        Detail = string.Join(", ", p.Groups.Select(g => g.Name)),
                        IsSelected = cutup.Sharing.Players.Any(s => s.Id == p.PlayerId)
                    })
                    .ToList()
            });   // Sharing View
        }

        // POST: Cutups/Sharing/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sharing(int id, bool wholeTeam, List<int> groupIds, List<int> playerIds)
        {
            var sharing = new SetCutupSharingDto { WholeTeam = wholeTeam, GroupIds = groupIds, PlayerIds = playerIds };

            var response = await _client.PutAsJsonAsync($"api/Cutup/{id}/sharing", sharing);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "Who the cutup is shared with was saved.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 for example a player who has been deactivated
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
