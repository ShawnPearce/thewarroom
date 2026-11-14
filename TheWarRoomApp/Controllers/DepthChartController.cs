using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The weekly depth chart. A change is live as soon as it is saved.
    public class DepthChartController : WarRoomController
    {
        private readonly HttpClient _client;

        public DepthChartController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: DepthChart?weekId=5&unit=Defence
        public async Task<IActionResult> Index(int? weekId, TeamUnit unit = TeamUnit.Offence)
        {
            var model = new DepthChartViewModel { SelectedUnit = unit };

            var weeksResponse = await _client.GetAsync("api/DepthChart/weeks");
            if (!weeksResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(weeksResponse);
            }
            model.Weeks = (await weeksResponse.Content.ReadFromJsonAsync<List<DepthChartWeekDto>>())!;

            // The chosen week, or this week's chart when none was chosen
            var chartResponse = weekId != null
                ? await _client.GetAsync($"api/DepthChart/weeks/{weekId}")
                : await _client.GetAsync("api/DepthChart/current");

            if (chartResponse.IsSuccessStatusCode)
            {
                model.Chart = await chartResponse.Content.ReadFromJsonAsync<DepthChartDto>();
            }
            else if (chartResponse.StatusCode != HttpStatusCode.NotFound || weekId != null)
            {
                // "current" answering 404 just means the team has no depth chart yet; anything else is a real failure
                return await ApiFailureAsync(chartResponse);
            }

            // A player also sees their own spots, pulled out of the full chart
            if (model.Chart != null && User.IsPlayer())
            {
                var mineResponse = await _client.GetAsync($"api/DepthChart/weeks/{model.Chart.Week.Id}/my");
                if (mineResponse.IsSuccessStatusCode)
                {
                    model.MyPlacements = (await mineResponse.Content.ReadFromJsonAsync<List<MyPlacementDto>>())!;
                }
            }

            return View(model);   // Index View
        }

        // GET: DepthChart/Position?weekId=5&positionId=12
        public async Task<IActionResult> Position(int weekId, int positionId)
        {
            var response = await _client.GetAsync($"api/DepthChart/weeks/{weekId}/positions/{positionId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 404 when the week or position is not on this team
            }

            var slot = await response.Content.ReadFromJsonAsync<DepthChartSlotDto>();
            return View(slot);   // Position View
        }

        // POST: DepthChart/AddPlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(int weekId, int positionId, int playerId)
        {
            var response = await _client.PostAsJsonAsync(
                $"api/DepthChart/weeks/{weekId}/positions/{positionId}/players",
                new AddPlacementDto { PlayerId = playerId });

            if (!response.IsSuccessStatusCode && !await StoreRefusalMessageAsync(response))   // 409 or 403 is shown on the page
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Position), new { weekId, positionId });
        }

        // POST: DepthChart/Move
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Move(int weekId, int positionId, int placementId, MoveDirection direction)
        {
            var response = await _client.PostAsJsonAsync(
                $"api/DepthChart/placements/{placementId}/move",
                new MovePlacementDto { Direction = direction });

            if (!response.IsSuccessStatusCode && !await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Position), new { weekId, positionId });
        }

        // POST: DepthChart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int weekId, int positionId, int placementId)
        {
            var response = await _client.DeleteAsync($"api/DepthChart/placements/{placementId}");

            if (!response.IsSuccessStatusCode && !await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Position), new { weekId, positionId });
        }

        // GET: DepthChart/NewWeek
        public async Task<IActionResult> NewWeek()
        {
            // The page is for the head coach and coordinators. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach() && !User.IsInRole(nameof(UserRole.Coordinator)))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var model = new NewWeekViewModel();
            var failure = await LoadWeeksAsync(model);
            if (failure != null)
            {
                return failure;
            }

            // Suggest the week after the latest chart (or this week when there is none), copied from the latest chart
            var latest = model.Weeks.FirstOrDefault();
            model.Week.WeekStartDate = latest != null
                ? latest.WeekStartDate.AddDays(7)
                : DateOnly.FromDateTime(DateTime.Today);
            model.Week.CopyFromWeekId = latest?.Id;

            return View(model);   // NewWeek View
        }

        // POST: DepthChart/NewWeek
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewWeek(NewWeekViewModel model)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PostAsJsonAsync("api/DepthChart/weeks", model.Week);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var created = await response.Content.ReadFromJsonAsync<DepthChartWeekDto>();
                    return RedirectToAction(nameof(Index), new { weekId = created!.Id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 that week already has a chart
                {
                    ModelState.AddModelError("Week.WeekStartDate", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            var failure = await LoadWeeksAsync(model);
            if (failure != null)
            {
                return failure;
            }

            return View(model);   // NewWeek View with errors
        }

        // POST: DepthChart/DeleteWeek
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWeek(int weekId)
        {
            var response = await _client.DeleteAsync($"api/DepthChart/weeks/{weekId}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The week's depth chart was deleted.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))
            {
                return RedirectToAction(nameof(Index), new { weekId });
            }

            return await ApiFailureAsync(response);
        }

        // Fills the earlier weeks for the "start from" list on the new-week form
        private async Task<IActionResult?> LoadWeeksAsync(NewWeekViewModel model)
        {
            var response = await _client.GetAsync("api/DepthChart/weeks");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            model.Weeks = (await response.Content.ReadFromJsonAsync<List<DepthChartWeekDto>>())!;
            return null;
        }
    }
}
