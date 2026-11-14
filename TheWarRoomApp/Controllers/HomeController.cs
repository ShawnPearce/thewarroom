using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    public class HomeController : WarRoomController
    {
        private readonly HttpClient _client;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: /
        [AllowAnonymous]
        public IActionResult Index()
        {
            // A signed-in user goes straight to their dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Dashboard));
            }

            return View();   // public landing page
        }

        // GET: Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var response = await _client.GetAsync("api/Auth/me");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var model = new DashboardViewModel
            {
                Me = (await response.Content.ReadFromJsonAsync<CurrentUserDto>())!
            };

            // This week's depth chart. 404 only means the team has not made one yet.
            var chartResponse = await _client.GetAsync("api/DepthChart/current");
            if (chartResponse.IsSuccessStatusCode)
            {
                var chart = await chartResponse.Content.ReadFromJsonAsync<DepthChartDto>();
                model.Week = chart!.Week;

                // A player sees where they stand this week
                if (model.Me.Role == UserRole.Player)
                {
                    var mineResponse = await _client.GetAsync($"api/DepthChart/weeks/{chart.Week.Id}/my");
                    if (mineResponse.IsSuccessStatusCode)
                    {
                        model.MyPlacements = (await mineResponse.Content.ReadFromJsonAsync<List<MyPlacementDto>>())!;
                    }
                }
            }

            // The next three events from today onwards
            var today = DateOnly.FromDateTime(DateTime.Today);
            var eventsResponse = await _client.GetAsync($"api/ScheduledEvent?from={today:yyyy-MM-dd}");
            if (eventsResponse.IsSuccessStatusCode)
            {
                var upcoming = await eventsResponse.Content.ReadFromJsonAsync<List<ScheduledEventDto>>();
                model.NextEvents = upcoming!.Take(3).ToList();
            }

            return View(model);   // Dashboard View
        }

        // GET: Home/Privacy
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
