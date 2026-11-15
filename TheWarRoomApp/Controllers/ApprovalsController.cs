using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Playbook changes: the ones waiting for the head coach's decision, and what became of earlier ones.
    // The head coach approves or rejects; the coach who asked can revise or withdraw while it is waiting,
    // and can revise a rejected change and send it again with the head coach's note beside the drawing.
    public class ApprovalsController : WarRoomController
    {
        private const int HistoryLength = 50;

        private readonly HttpClient _client;

        public ApprovalsController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Approvals?show=history
        public async Task<IActionResult> Index(string? show)
        {
            // The page is for coaches. The API refuses players too.
            if (User.IsPlayer())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync("api/ApprovalRequest");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            // Newest first, as the API returns them
            var requests = (await response.Content.ReadFromJsonAsync<List<ApprovalRequestDto>>())!;

            // A coach's own changes that the head coach rejected stay in sight while they are news to the coach
            // or can still be revised and sent again. The head coach publishes directly, so has none.
            var sentBack = requests.Where(r => r.CanResubmit || r.IsNewDecision).ToList();

            // Without a choice the page opens on what was sent back when there is news, and otherwise on what is waiting
            if (show != "history" && show != "sent-back" && show != "waiting")
            {
                show = sentBack.Any(r => r.IsNewDecision) ? "sent-back" : "waiting";
            }
            if (show == "sent-back" && User.IsHeadCoach())
            {
                show = "waiting";
            }

            return View(new ApprovalsViewModel
            {
                Show = show,
                Waiting = requests.Where(r => r.Status == ApprovalStatus.Pending).ToList(),
                SentBack = sentBack,
                History = requests.Where(r => r.Status != ApprovalStatus.Pending).Take(HistoryLength).ToList()
            });   // Index View
        }

        // GET: Approvals/Details/5   (the change, with what it replaces beside what is proposed)
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/ApprovalRequest/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var request = (await response.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>())!;

            // The coach who asked has now read the head coach's rejection, so it stops being counted as news
            await MarkSeenAsync(request);

            return View(request);   // Details View
        }

        // POST: Approvals/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, ReviewDecisionDto decision)
        {
            if (!ModelState.IsValid)   // the note is too long
            {
                TempData["Error"] = "The note can't exceed 500 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var response = await _client.PostAsJsonAsync($"api/ApprovalRequest/{id}/approve", decision);
            if (response.IsSuccessStatusCode)   // 204 No Content: the change is live
            {
                TempData["Message"] = "The change was approved. Everyone sees it now.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 already decided, or the playbook changed in the meantime
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // POST: Approvals/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, ReviewDecisionDto decision)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "The note can't exceed 500 characters.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var response = await _client.PostAsJsonAsync($"api/ApprovalRequest/{id}/reject", decision);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The change was rejected. The playbook stays as it is.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 already decided
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // POST: Approvals/Withdraw/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id)
        {
            var response = await _client.PostAsync($"api/ApprovalRequest/{id}/withdraw", null);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "Your request was withdrawn.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 already decided; 403 somebody else's request
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // GET: Approvals/Revise/5   (the designer, opened on what was proposed)
        public async Task<IActionResult> Revise(int id)
        {
            var response = await _client.GetAsync($"api/ApprovalRequest/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            // Only the coach who made a request can redraw it, and only while it is waiting.
            // A request to delete has no drawing. The API enforces all of this when the drawing is saved.
            var request = (await response.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>())!;
            if (!request.CanRevise || request.Action == PlaybookChangeAction.Delete)
            {
                TempData["Error"] = "This request cannot be revised.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View("Designer", new DesignerViewModel
            {
                Name = request.ProposedName ?? request.TargetName,
                DiagramJson = request.ProposedJson ?? string.Empty,
                Notes = request.ProposedNotes,
                DraftKey = $"request-{request.Id}",
                Page = RevisePage(request)
            });
        }

        // POST: Approvals/Revise/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revise(int id, DesignerViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var revision = new ReviseApprovalRequestDto
                {
                    Name = model.Name,
                    DiagramJson = model.DiagramJson,
                    Notes = model.Notes
                };

                var response = await _client.PutAsJsonAsync($"api/ApprovalRequest/{id}", revision);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    // The drawing is saved, so the copy the designer kept in the browser can go (see site.js)
                    TempData["ClearDraft"] = model.DraftKey;
                    TempData["Message"] = "Your request was updated. It is still waiting for the head coach.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 a rule was broken, or it was decided meanwhile
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            var current = await _client.GetAsync($"api/ApprovalRequest/{id}");
            if (!current.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(current);
            }

            model.Page = RevisePage((await current.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>())!);
            return View("Designer", model);   // Designer View with errors
        }

        // GET: Approvals/Resubmit/5   (the designer, opened on the drawing the head coach rejected, with the note beside it)
        public async Task<IActionResult> Resubmit(int id)
        {
            var response = await _client.GetAsync($"api/ApprovalRequest/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            // Only the coach who made a request can send it again, and only a rejected one that has a drawing,
            // was not sent again already and still fits the playbook. The API enforces all of this when it is saved.
            var request = (await response.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>())!;
            if (!request.CanResubmit)
            {
                TempData["Error"] = "This request cannot be sent again.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await MarkSeenAsync(request);

            return View("Designer", new DesignerViewModel
            {
                Name = request.ProposedName ?? request.TargetName,
                DiagramJson = request.ProposedJson ?? string.Empty,
                Notes = request.ProposedNotes,
                DraftKey = $"resubmit-{request.Id}",
                Page = ResubmitPage(request)
            });
        }

        // POST: Approvals/Resubmit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resubmit(int id, DesignerViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var revision = new ReviseApprovalRequestDto
                {
                    Name = model.Name,
                    DiagramJson = model.DiagramJson,
                    Notes = model.Notes
                };

                var response = await _client.PostAsJsonAsync($"api/ApprovalRequest/{id}/resubmit", revision);
                if (response.IsSuccessStatusCode)   // 202 Accepted: waiting again. 200 OK: this coach may publish by now.
                {
                    var result = (await response.Content.ReadFromJsonAsync<PlaybookChangeResultDto>())!;

                    // The drawing is saved, so the copy the designer kept in the browser can go (see site.js)
                    TempData["ClearDraft"] = model.DraftKey;
                    TempData["Message"] = result.Published
                        ? "Your change was saved. Everyone sees it now."
                        : "Your new version was sent to the head coach for approval. Players keep seeing the current playbook until it is approved.";
                    return RedirectToAction(nameof(Details), new { id = result.RequestId });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 a rule was broken, or the playbook changed meanwhile
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            var current = await _client.GetAsync($"api/ApprovalRequest/{id}");
            if (!current.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(current);
            }

            model.Page = ResubmitPage((await current.Content.ReadFromJsonAsync<ApprovalRequestDetailDto>())!);
            return View("Designer", model);   // Designer View with errors
        }

        // Tells the API that the coach who made the request has read the head coach's rejection.
        // Nothing depends on the answer: at worst the request is counted as news once more.
        private async Task MarkSeenAsync(ApprovalRequestDto request)
        {
            if (request.IsNewDecision)
            {
                await _client.PostAsync($"api/ApprovalRequest/{request.Id}/seen", null);
            }
        }

        private DesignerPage ResubmitPage(ApprovalRequestDetailDto request)
        {
            string change = $"{DisplayText.Change(request.Action, request.TargetType)}: {request.TargetName}";

            return new DesignerPage
            {
                Heading = "Revise and send again",
                IsFormation = request.TargetType == PlaybookTargetType.Formation,
                Unit = request.Unit,
                CanPublish = request.CanPublish,
                IsResubmission = true,
                Feedback = request.ReviewNote,
                FeedbackFrom = request.ReviewedBy,
                FeedbackAt = request.ReviewedAt,
                Trail = new List<PageLink>
                {
                    new PageLink("Playbook changes", Url.Action(nameof(Index))),
                    new PageLink(change, Url.Action(nameof(Details), new { id = request.Id })),
                    new PageLink("Send again", null)
                },
                CancelUrl = Url.Action(nameof(Details), new { id = request.Id })!
            };
        }

        private DesignerPage RevisePage(ApprovalRequestDetailDto request)
        {
            string change = $"{DisplayText.Change(request.Action, request.TargetType)}: {request.TargetName}";

            return new DesignerPage
            {
                Heading = "Revise your request",
                IsFormation = request.TargetType == PlaybookTargetType.Formation,
                Unit = request.Unit,
                IsRevision = true,
                Trail = new List<PageLink>
                {
                    new PageLink("Playbook changes", Url.Action(nameof(Index))),
                    new PageLink(change, Url.Action(nameof(Details), new { id = request.Id })),
                    new PageLink("Revise", null)
                },
                CancelUrl = Url.Action(nameof(Details), new { id = request.Id })!
            };
        }
    }
}
