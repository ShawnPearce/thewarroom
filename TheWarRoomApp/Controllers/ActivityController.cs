using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Study time: how long players were signed in, watched film and read the playbook.
    // Coaches see the players of their groups; a player sees their own numbers.
    public class ActivityController : WarRoomController
    {
        private const int WeeksShown = 8;

        private readonly HttpClient _client;

        public ActivityController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Activity?weekOf=2026-11-09   (the team's players for one week)
        public async Task<IActionResult> Index(DateOnly? weekOf)
        {
            // A player's page is their own activity
            if (User.IsPlayer())
            {
                return RedirectToAction(nameof(Mine));
            }

            string url = weekOf != null ? $"api/Activity?weekOf={weekOf:yyyy-MM-dd}" : "api/Activity";
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var report = (await response.Content.ReadFromJsonAsync<ActivityReportDto>())!;

            // This week's Monday: asking the API without a date returns it
            var thisWeek = report.WeekStart;
            if (weekOf != null)
            {
                var currentResponse = await _client.GetAsync("api/Activity");
                if (currentResponse.IsSuccessStatusCode)
                {
                    thisWeek = (await currentResponse.Content.ReadFromJsonAsync<ActivityReportDto>())!.WeekStart;
                }
            }

            return View(new ActivityIndexViewModel { Report = report, ThisWeek = thisWeek });   // Index View
        }

        // GET: Activity/Player/14   (one player, week by week)
        public async Task<IActionResult> Player(int id)
        {
            var response = await _client.GetAsync($"api/Activity/player/{id}?weeks={WeeksShown}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 403 when the player is outside the coach's groups
            }

            var activity = await response.Content.ReadFromJsonAsync<PlayerActivityDto>();
            return View(activity);   // Player View
        }

        // GET: Activity/Mine   (the signed-in player's own study time)
        public async Task<IActionResult> Mine()
        {
            // Coaches have no study time of their own; their page is the team's
            if (User.IsCoach())
            {
                return RedirectToAction(nameof(Index));
            }

            var response = await _client.GetAsync($"api/Activity/me?weeks={WeeksShown}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var activity = await response.Content.ReadFromJsonAsync<PlayerActivityDto>();
            return View("Player", activity);   // Player View, showing the player's own numbers
        }
    }
}
