using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The playbooks: their formations and plays, and the designer that draws them.
    // Everyone reads the approved content. A coach's change either goes live or is sent to the head coach:
    // the API decides which, and answers 200 OK or 202 Accepted.
    public class PlaybookController : WarRoomController
    {
        private const string WaitingMessage =
            "Your change was sent to the head coach for approval. Players keep seeing the current version until it is approved.";

        private readonly HttpClient _client;

        public PlaybookController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Playbook
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/Playbook");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var playbooks = await response.Content.ReadFromJsonAsync<List<PlaybookDto>>();
            return View(playbooks);   // Index View
        }

        // GET: Playbook/Details/5   (a playbook with its formations)
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/Playbook/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 403 when a player is not in a group of this unit
            }

            var playbook = await response.Content.ReadFromJsonAsync<PlaybookDetailDto>();
            return View(playbook);   // Details View
        }

        // GET: Playbook/Formation/5   (a formation with its plays)
        public async Task<IActionResult> Formation(int id)
        {
            var response = await _client.GetAsync($"api/Formation/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var formation = await response.Content.ReadFromJsonAsync<FormationDto>();
            return View(formation);   // Formation View
        }

        // GET: Playbook/Play/5   (always the last approved version)
        public async Task<IActionResult> Play(int id)
        {
            var response = await _client.GetAsync($"api/Play/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var play = await response.Content.ReadFromJsonAsync<PlayDto>();
            return View(play);   // Play View
        }

        // ---------- Formations ----------

        // GET: Playbook/NewFormation?playbookId=1&startFrom=4
        public async Task<IActionResult> NewFormation(int playbookId, int? startFrom)
        {
            var response = await _client.GetAsync($"api/Playbook/{playbookId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            // The page is for coaches of this playbook. The API enforces this too when the drawing is saved.
            var playbook = (await response.Content.ReadFromJsonAsync<PlaybookDetailDto>())!;
            if (!playbook.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // The drawing starts as a copy of the chosen formation, or as an empty field
            var start = playbook.Formations.FirstOrDefault(f => f.Id == startFrom);

            return View("Designer", new DesignerViewModel
            {
                DiagramJson = start?.DiagramJson ?? EmptyDiagram(playbook.Unit, playbook.Rules),
                DraftKey = $"new-formation-{playbook.Id}",
                Page = NewFormationPage(playbook, start?.Id)
            });
        }

        // POST: Playbook/NewFormation?playbookId=1   (also where "Save as new formation" posts to)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewFormation(int playbookId, int? startFrom, DesignerViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var formation = new SaveFormationDto
                {
                    PlaybookId = playbookId,
                    Name = model.Name,
                    DiagramJson = model.DiagramJson
                };

                var response = await _client.PostAsJsonAsync("api/Formation", formation);
                var next = await AfterChangeAsync(response, model, "The formation was added to the playbook.",
                    result => RedirectToAction(nameof(Formation), new { id = result.FormationId }));
                if (next != null)
                {
                    return next;
                }
            }

            // Designer View with errors: the drawing comes back from the form, the rest of the page is read again
            var playbookResponse = await _client.GetAsync($"api/Playbook/{playbookId}");
            if (!playbookResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(playbookResponse);
            }

            var playbook = (await playbookResponse.Content.ReadFromJsonAsync<PlaybookDetailDto>())!;
            model.Page = NewFormationPage(playbook, startFrom);
            return View("Designer", model);
        }

        // GET: Playbook/EditFormation/5
        public async Task<IActionResult> EditFormation(int id)
        {
            var response = await _client.GetAsync($"api/Formation/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var formation = (await response.Content.ReadFromJsonAsync<FormationDto>())!;
            if (!formation.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // One change at a time may be waiting on a formation, so there is nothing to draw until it is decided
            if (formation.PendingRequestId != null)
            {
                TempData["Error"] = $"A change to {formation.Name} is already waiting for approval.";
                return RedirectToAction(nameof(Formation), new { id });
            }

            return View("Designer", new DesignerViewModel
            {
                Name = formation.Name,
                DiagramJson = formation.DiagramJson,
                Version = formation.Version,
                DraftKey = $"formation-{formation.Id}-v{formation.Version}",
                Page = EditFormationPage(formation)
            });
        }

        // POST: Playbook/EditFormation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditFormation(int id, DesignerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var formation = new SaveFormationDto
                {
                    Name = model.Name,
                    DiagramJson = model.DiagramJson,
                    Version = model.Version
                };

                var response = await _client.PutAsJsonAsync($"api/Formation/{id}", formation);
                var next = await AfterChangeAsync(response, model, "The formation was saved. Everyone sees the new version now.",
                    result => RedirectToAction(nameof(Formation), new { id }));
                if (next != null)
                {
                    return next;
                }
            }

            var current = await _client.GetAsync($"api/Formation/{id}");
            if (!current.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(current);
            }

            model.Page = EditFormationPage((await current.Content.ReadFromJsonAsync<FormationDto>())!);
            return View("Designer", model);   // Designer View with errors
        }

        // GET: Playbook/DeleteFormation/5
        public async Task<IActionResult> DeleteFormation(int id)
        {
            var response = await _client.GetAsync($"api/Formation/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var formation = (await response.Content.ReadFromJsonAsync<FormationDto>())!;
            if (!formation.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(formation);   // Delete confirmation View
        }

        // POST: Playbook/DeleteFormation/5
        [HttpPost, ActionName("DeleteFormation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFormationConfirmed(int id, int playbookId)
        {
            var response = await _client.DeleteAsync($"api/Formation/{id}");
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Accepted)   // 202 Accepted: waiting for the head coach
                {
                    var result = (await response.Content.ReadFromJsonAsync<PlaybookChangeResultDto>())!;
                    TempData["Message"] = "Your request to delete the formation was sent to the head coach for approval.";
                    return RedirectToAction("Details", "Approvals", new { id = result.RequestId });
                }

                TempData["Message"] = "The formation was deleted.";   // 200 OK
                return RedirectToAction(nameof(Details), new { id = playbookId });
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 it still has plays, or a change is already waiting
            {
                return RedirectToAction(nameof(Formation), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- Plays ----------

        // GET: Playbook/NewPlay?formationId=3
        public async Task<IActionResult> NewPlay(int formationId)
        {
            var response = await _client.GetAsync($"api/Formation/{formationId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var formation = (await response.Content.ReadFromJsonAsync<FormationDto>())!;
            if (!formation.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // A new play starts as its formation: the players are in place and the routes are drawn on top
            return View("Designer", new DesignerViewModel
            {
                DiagramJson = formation.DiagramJson,
                DraftKey = $"new-play-{formation.Id}",
                Page = NewPlayPage(formation)
            });
        }

        // POST: Playbook/NewPlay?formationId=3
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewPlay(int formationId, DesignerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var play = new SavePlayDto
                {
                    FormationId = formationId,
                    Name = model.Name,
                    DiagramJson = model.DiagramJson,
                    Notes = model.Notes
                };

                var response = await _client.PostAsJsonAsync("api/Play", play);
                var next = await AfterChangeAsync(response, model, "The play was added to the playbook.",
                    result => RedirectToAction(nameof(Play), new { id = result.PlayId }));
                if (next != null)
                {
                    return next;
                }
            }

            var formationResponse = await _client.GetAsync($"api/Formation/{formationId}");
            if (!formationResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(formationResponse);
            }

            model.Page = NewPlayPage((await formationResponse.Content.ReadFromJsonAsync<FormationDto>())!);
            return View("Designer", model);   // Designer View with errors
        }

        // GET: Playbook/EditPlay/5
        public async Task<IActionResult> EditPlay(int id)
        {
            var response = await _client.GetAsync($"api/Play/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var play = (await response.Content.ReadFromJsonAsync<PlayDto>())!;
            if (!play.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // One change at a time may be waiting on a play, so there is nothing to draw until it is decided
            if (play.PendingRequestId != null)
            {
                TempData["Error"] = $"A change to {play.Name} is already waiting for approval.";
                return RedirectToAction(nameof(Play), new { id });
            }

            return View("Designer", new DesignerViewModel
            {
                Name = play.Name,
                DiagramJson = play.DiagramJson,
                Notes = play.Notes,
                Version = play.Version,
                DraftKey = $"play-{play.Id}-v{play.Version}",
                Page = EditPlayPage(play)
            });
        }

        // POST: Playbook/EditPlay/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPlay(int id, DesignerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var play = new SavePlayDto
                {
                    Name = model.Name,
                    DiagramJson = model.DiagramJson,
                    Notes = model.Notes,
                    Version = model.Version
                };

                var response = await _client.PutAsJsonAsync($"api/Play/{id}", play);
                var next = await AfterChangeAsync(response, model, "The play was saved. Everyone sees the new version now.",
                    result => RedirectToAction(nameof(Play), new { id }));
                if (next != null)
                {
                    return next;
                }
            }

            var current = await _client.GetAsync($"api/Play/{id}");
            if (!current.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(current);
            }

            model.Page = EditPlayPage((await current.Content.ReadFromJsonAsync<PlayDto>())!);
            return View("Designer", model);   // Designer View with errors
        }

        // GET: Playbook/DeletePlay/5
        public async Task<IActionResult> DeletePlay(int id)
        {
            var response = await _client.GetAsync($"api/Play/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var play = (await response.Content.ReadFromJsonAsync<PlayDto>())!;
            if (!play.CanChange)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(play);   // Delete confirmation View
        }

        // POST: Playbook/DeletePlay/5
        [HttpPost, ActionName("DeletePlay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlayConfirmed(int id, int formationId)
        {
            var response = await _client.DeleteAsync($"api/Play/{id}");
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Accepted)   // 202 Accepted: waiting for the head coach
                {
                    var result = (await response.Content.ReadFromJsonAsync<PlaybookChangeResultDto>())!;
                    TempData["Message"] = "Your request to delete the play was sent to the head coach for approval.";
                    return RedirectToAction("Details", "Approvals", new { id = result.RequestId });
                }

                TempData["Message"] = "The play was deleted.";   // 200 OK
                return RedirectToAction(nameof(Formation), new { id = formationId });
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 a change to the play is already waiting
            {
                return RedirectToAction(nameof(Play), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- Helpers ----------

        // What the API answered to a new or changed formation or play, turned into the next page:
        //   200 OK         the change is live              -> the page given by whenLive
        //   202 Accepted   it waits for the head coach     -> the page of the request
        //   409 Conflict   a rule was broken               -> null: the message is added to the form, which is shown again
        private async Task<IActionResult?> AfterChangeAsync(HttpResponseMessage response, DesignerViewModel model,
            string liveMessage, Func<PlaybookChangeResultDto, IActionResult> whenLive)
        {
            if (response.IsSuccessStatusCode)
            {
                var result = (await response.Content.ReadFromJsonAsync<PlaybookChangeResultDto>())!;

                // The drawing is saved, so the copy the designer kept in the browser can go (see site.js)
                TempData["ClearDraft"] = model.DraftKey;

                if (response.StatusCode == HttpStatusCode.Accepted)   // 202 Accepted
                {
                    TempData["Message"] = WaitingMessage;
                    return RedirectToAction("Details", "Approvals", new { id = result.RequestId });
                }

                TempData["Message"] = liveMessage;   // 200 OK
                return whenLive(result);
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 Conflict
            {
                ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                return null;
            }

            return await ApiFailureAsync(response);
        }

        // An empty field for a unit: the ball on the line of scrimmage and nobody lined up yet.
        // The game decides how wide the field is drawn and how many players a side may be put on it.
        private static string EmptyDiagram(TeamUnit unit, FootballRules rules)
        {
            // The defence lines up past the line of scrimmage, so its field shows more of that side
            bool defence = unit == TeamUnit.Defence;

            return JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                unit = unit.ToString(),
                rules = rules.ToString(),
                view = new { yMin = defence ? -8 : -12, yMax = defence ? 22 : 18 },
                ballX = 0,
                players = Array.Empty<object>(),
                drawings = Array.Empty<object>()
            });
        }

        private DesignerPage NewFormationPage(PlaybookDetailDto playbook, int? startFrom)
        {
            return new DesignerPage
            {
                Heading = "New formation",
                IsFormation = true,
                Unit = playbook.Unit,
                CanPublish = playbook.CanPublish,
                Trail = Trail(playbook.Id, playbook.Name, "New formation"),
                CancelUrl = Url.Action(nameof(Details), new { id = playbook.Id })!,
                NewFormationPlaybookId = playbook.Id,
                StartFrom = startFrom,
                StartingPoints = playbook.Formations
            };
        }

        private DesignerPage EditFormationPage(FormationDto formation)
        {
            var trail = Trail(formation.PlaybookId, formation.PlaybookName, "Edit formation");
            trail.Insert(2, new PageLink(formation.Name, Url.Action(nameof(Formation), new { id = formation.Id })));

            return new DesignerPage
            {
                Heading = $"Edit {formation.Name}",
                IsFormation = true,
                Unit = formation.Unit,
                CanPublish = formation.CanPublish,
                Trail = trail,
                CancelUrl = Url.Action(nameof(Formation), new { id = formation.Id })!,
                SaveAsNewUrl = Url.Action(nameof(NewFormation), new { playbookId = formation.PlaybookId })
            };
        }

        private DesignerPage NewPlayPage(FormationDto formation)
        {
            var trail = Trail(formation.PlaybookId, formation.PlaybookName, "New play");
            trail.Insert(2, new PageLink(formation.Name, Url.Action(nameof(Formation), new { id = formation.Id })));

            return new DesignerPage
            {
                Heading = $"New play from {formation.Name}",
                Unit = formation.Unit,
                CanPublish = formation.CanPublish,
                Trail = trail,
                CancelUrl = Url.Action(nameof(Formation), new { id = formation.Id })!
            };
        }

        private DesignerPage EditPlayPage(PlayDto play)
        {
            var trail = Trail(play.PlaybookId, play.PlaybookName, "Edit play");
            trail.Insert(2, new PageLink(play.FormationName, Url.Action(nameof(Formation), new { id = play.FormationId })));
            trail.Insert(3, new PageLink(play.Name, Url.Action(nameof(Play), new { id = play.Id })));

            return new DesignerPage
            {
                Heading = $"Edit {play.Name}",
                Unit = play.Unit,
                CanPublish = play.CanPublish,
                Trail = trail,
                CancelUrl = Url.Action(nameof(Play), new { id = play.Id })!
            };
        }

        // The links at the top of a designer page: the playbooks, this playbook, and the page itself last
        private List<PageLink> Trail(int playbookId, string playbookName, string currentPage)
        {
            return new List<PageLink>
            {
                new PageLink("Playbook", Url.Action(nameof(Index))),
                new PageLink(playbookName, Url.Action(nameof(Details), new { id = playbookId })),
                new PageLink(currentPage, null)
            };
        }
    }
}
