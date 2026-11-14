using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The teams that appear on film: the team itself, and the opponents it scouts
    public class ScoutedTeamsController : WarRoomController
    {
        private readonly HttpClient _client;

        public ScoutedTeamsController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: ScoutedTeams
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/ScoutedTeam");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 403 for players: game film is for coaches
            }

            var teams = await response.Content.ReadFromJsonAsync<List<ScoutedTeamDto>>();
            return View(teams);   // Index View
        }

        // GET: ScoutedTeams/Create
        public IActionResult Create()
        {
            // The page is for coaches. The API enforces this too when the form is saved.
            if (User.IsPlayer())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View();   // Create View
        }

        // POST: ScoutedTeams/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaveScoutedTeamDto team)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/ScoutedTeam", team);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the name is already used
                {
                    ModelState.AddModelError("Name", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View(team);   // Create View with errors
        }

        // GET: ScoutedTeams/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var response = await _client.GetAsync($"api/ScoutedTeam/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var team = (await response.Content.ReadFromJsonAsync<ScoutedTeamDto>())!;

            // The team's own entry carries the team's name, which is changed in the team settings
            if (team.IsOwnTeam)
            {
                TempData["Error"] = "Your own team is renamed in the team settings. Its name on film follows.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["TeamId"] = id;
            return View(new SaveScoutedTeamDto { Name = team.Name });   // Edit View
        }

        // POST: ScoutedTeams/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SaveScoutedTeamDto team)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/ScoutedTeam/{id}", team);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the name is already used
                {
                    ModelState.AddModelError("Name", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            ViewData["TeamId"] = id;
            return View(team);   // Edit View with errors
        }

        // GET: ScoutedTeams/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _client.GetAsync($"api/ScoutedTeam/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var team = await response.Content.ReadFromJsonAsync<ScoutedTeamDto>();
            return View(team);   // Delete confirmation View
        }

        // POST: ScoutedTeams/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var response = await _client.DeleteAsync($"api/ScoutedTeam/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The team was removed.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 the team has games on film, or it is the team itself
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
