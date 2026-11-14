using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The players of the team
    public class RosterController : WarRoomController
    {
        private readonly HttpClient _client;

        public RosterController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Roster
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/Roster");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var roster = await response.Content.ReadFromJsonAsync<List<RosterEntryDto>>();
            return View(roster);   // Index View
        }

        // GET: Roster/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/Roster/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 404 when the player is not on this team
            }

            var model = new RosterDetailsViewModel
            {
                Player = (await response.Content.ReadFromJsonAsync<RosterEntryDto>())!
            };

            // A player may only see their own coaches, so for a teammate this answers 403 and the list is left out
            var coachesResponse = await _client.GetAsync($"api/Roster/{id}/coaches");
            if (coachesResponse.IsSuccessStatusCode)
            {
                model.Coaches = await coachesResponse.Content.ReadFromJsonAsync<List<CoachSummaryDto>>();
            }

            return View(model);   // Details View
        }

        // GET: Roster/Create
        public async Task<IActionResult> Create()
        {
            // The page is for the head coach. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var model = new PlayerCreateViewModel();
            var failure = await LoadGroupsAsync(model);
            if (failure != null)
            {
                return failure;
            }

            return View(model);   // Create View
        }

        // POST: Roster/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlayerCreateViewModel model)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/Roster", model.Player);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var created = await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>();
                    KeepTemporaryPassword(created!);
                    return RedirectToAction(nameof(TemporaryPassword));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 email already used
                {
                    ModelState.AddModelError("Player.Email", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            // The list of groups is not posted back, so it is loaded again
            var failure = await LoadGroupsAsync(model);
            if (failure != null)
            {
                return failure;
            }

            return View(model);   // Create View with errors
        }

        // GET: Roster/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            // The page is for the head coach and coordinators. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach() && !User.IsInRole(nameof(UserRole.Coordinator)))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync($"api/Roster/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var player = await response.Content.ReadFromJsonAsync<RosterEntryDto>();

            var model = new PlayerEditViewModel
            {
                PlayerId = player!.PlayerId,
                CanEditAccount = User.IsHeadCoach(),   // only the head coach changes names and emails
                FirstName = player.FirstName,
                LastName = player.LastName,
                Email = player.Email,
                JerseyNumber = player.JerseyNumber,
                Grade = player.Grade,
                HeightInches = player.HeightInches,
                WeightLbs = player.WeightLbs,
                Status = player.Status
            };

            return View(model);   // Edit View
        }

        // POST: Roster/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlayerEditViewModel model)
        {
            if (id != model.PlayerId)   // id in URL must match the form
            {
                return BadRequest();
            }

            model.CanEditAccount = User.IsHeadCoach();

            if (model.CanEditAccount)
            {
                // The name and email fields are on the form for the head coach, so they are required
                if (string.IsNullOrWhiteSpace(model.FirstName))
                {
                    ModelState.AddModelError("FirstName", "First name is required.");
                }
                if (string.IsNullOrWhiteSpace(model.LastName))
                {
                    ModelState.AddModelError("LastName", "Last name is required.");
                }
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    ModelState.AddModelError("Email", "Email is required.");
                }
            }

            if (ModelState.IsValid)
            {
                // 1. The account details (head coach only)
                if (model.CanEditAccount)
                {
                    var account = new UpdateUserDto
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email!,
                        Role = UserRole.Player
                    };

                    var accountResponse = await _client.PutAsJsonAsync($"api/User/{id}", account);
                    if (accountResponse.StatusCode == HttpStatusCode.Conflict)   // 409 email already used
                    {
                        ModelState.AddModelError("Email", await accountResponse.Content.ReadAsStringAsync());
                        return View(model);
                    }
                    else if (!accountResponse.IsSuccessStatusCode)
                    {
                        return await ApiFailureAsync(accountResponse);
                    }
                }

                // 2. The football details
                var details = new UpdateRosterEntryDto
                {
                    JerseyNumber = model.JerseyNumber,
                    Grade = model.Grade,
                    HeightInches = model.HeightInches,
                    WeightLbs = model.WeightLbs,
                    Status = model.Status
                };

                var response = await _client.PutAsJsonAsync($"api/Roster/{id}", details);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    TempData["Message"] = "The player was saved.";
                    return RedirectToAction(nameof(Details), new { id });
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

            return View(model);   // Edit View with errors
        }

        // POST: Roster/SetActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id, bool isActive)
        {
            var response = await _client.PutAsJsonAsync($"api/User/{id}/active", new SetActiveDto { IsActive = isActive });
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = isActive ? "The account was reactivated." : "The account was deactivated.";
            }
            else if (!await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Roster/ResetPassword/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var response = await _client.PostAsync($"api/User/{id}/reset-password", null);
            if (response.IsSuccessStatusCode)   // 200 OK with the new temporary password
            {
                var reset = await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>();
                KeepTemporaryPassword(reset!);
                return RedirectToAction(nameof(TemporaryPassword));
            }
            else if (await StoreRefusalMessageAsync(response))
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // GET: Roster/TemporaryPassword   (shown once, straight after adding a player or resetting a password)
        public IActionResult TemporaryPassword()
        {
            var temporaryPassword = TakeTemporaryPassword();
            if (temporaryPassword == null)
            {
                return RedirectToAction(nameof(Index));
            }

            ViewData["BackController"] = "Roster";
            return View("TemporaryPassword", temporaryPassword);
        }

        // Fills the position groups for the checkboxes on the add-player form
        private async Task<IActionResult?> LoadGroupsAsync(PlayerCreateViewModel model)
        {
            var response = await _client.GetAsync("api/PositionGroup");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            model.Groups = (await response.Content.ReadFromJsonAsync<List<PositionGroupDto>>())!;
            return null;
        }
    }
}
