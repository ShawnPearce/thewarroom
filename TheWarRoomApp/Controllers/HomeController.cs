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

            var me = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
            return View(me);   // Dashboard View
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
