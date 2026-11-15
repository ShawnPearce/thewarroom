using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    public class TeamController : WarRoomController
    {
        private readonly HttpClient _client;

        public TeamController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Team/Settings
        public async Task<IActionResult> Settings()
        {
            // The page is for the head coach. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync("api/Team");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var team = await response.Content.ReadFromJsonAsync<TeamDto>();
            return View(new UpdateTeamDto
            {
                Name = team!.Name,
                AttendanceTrackingEnabled = team.AttendanceTrackingEnabled,
                Rules = team.Rules
            });   // Settings View
        }

        // POST: Team/Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(UpdateTeamDto team)
        {
            if (ModelState.IsValid)
            {
                // The game the team plays before the save, so that the message can say when it was changed
                FootballRules? gameBefore = null;
                var current = await _client.GetAsync("api/Team");
                if (current.IsSuccessStatusCode)
                {
                    gameBefore = (await current.Content.ReadFromJsonAsync<TeamDto>())!.Rules;
                }

                var response = await _client.PutAsJsonAsync("api/Team", team);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    // The team name is also shown from the login cookie, so the cookie is issued again with the new name
                    await UpdateTeamNameInCookieAsync(team.Name.Trim());

                    TempData["Message"] = gameBefore != null && gameBefore != team.Rules
                        ? $"Team settings saved. The team now plays {DisplayText.Rules(team.Rules)}: the depth chart and the playbooks are set up for "
                            + $"{(team.Rules == FootballRules.Canadian ? 12 : 11)} players a side. What belongs only to the other game is put away, not deleted."
                        : "Team settings saved.";
                    return RedirectToAction(nameof(Settings));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409
                {
                    ModelState.AddModelError("Name", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View(team);   // Settings View with errors
        }

        private async Task UpdateTeamNameInCookieAsync(string teamName)
        {
            var claims = User.Claims
                .Where(c => c.Type != WarRoomClaimTypes.TeamName)
                .ToList();
            claims.Add(new Claim(WarRoomClaimTypes.TeamName, teamName));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}
