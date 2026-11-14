using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The coaching staff: listing coaches, and (for the head coach) adding and managing their accounts
    public class StaffController : WarRoomController
    {
        private readonly HttpClient _client;

        public StaffController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Staff
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/User");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var staff = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            return View(staff);   // Index View
        }

        // GET: Staff/Create
        public IActionResult Create()
        {
            // The page is for the head coach. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(new CreateCoachDto { Role = UserRole.PositionCoach });
        }

        // POST: Staff/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCoachDto coach)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/User", coach);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var created = await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>();
                    KeepTemporaryPassword(created!);
                    return RedirectToAction(nameof(TemporaryPassword));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 a rule was broken (email used, no unit chosen...)
                {
                    ModelState.AddModelError(string.Empty, await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View(coach);   // Create View with errors
        }

        // GET: Staff/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            // The page is for the head coach. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync($"api/User/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 404 when the account is not on this team
            }

            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            return View(ToEditViewModel(user!));   // Edit View
        }

        // POST: Staff/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaffEditViewModel model)
        {
            if (id != model.Id)   // id in URL must match the form
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var dto = new UpdateUserDto
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Role = model.Role,
                    CoordinatorUnit = model.Role == UserRole.Coordinator ? model.CoordinatorUnit : null
                };

                var response = await _client.PutAsJsonAsync($"api/User/{id}", dto);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    TempData["Message"] = $"{model.FirstName} {model.LastName} was saved.";
                    return RedirectToAction(nameof(Index));
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

            // The parts of the page that are not in the form have to be loaded again
            var reload = await _client.GetAsync($"api/User/{id}");
            if (!reload.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(reload);
            }

            var current = ToEditViewModel((await reload.Content.ReadFromJsonAsync<UserDto>())!);
            current.FirstName = model.FirstName;
            current.LastName = model.LastName;
            current.Email = model.Email;
            current.Role = model.Role;
            current.CoordinatorUnit = model.CoordinatorUnit;
            return View(current);   // Edit View with errors
        }

        // POST: Staff/Permissions/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Permissions(int id, [Bind(Prefix = "Permissions")] UpdatePermissionsDto permissions)
        {
            var response = await _client.PutAsJsonAsync($"api/User/{id}/permissions", permissions);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "Permissions saved.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 or 403 is shown on the next page
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Edit), new { id });
        }

        // POST: Staff/SetActive/5
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

            return RedirectToAction(nameof(Index));
        }

        // POST: Staff/ResetPassword/5
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
                return RedirectToAction(nameof(Index));
            }

            return await ApiFailureAsync(response);
        }

        // GET: Staff/TemporaryPassword   (shown once, straight after creating an account or resetting a password)
        public IActionResult TemporaryPassword()
        {
            var temporaryPassword = TakeTemporaryPassword();
            if (temporaryPassword == null)
            {
                return RedirectToAction(nameof(Index));
            }

            ViewData["BackController"] = "Staff";
            return View("TemporaryPassword", temporaryPassword);
        }

        private static StaffEditViewModel ToEditViewModel(UserDto user)
        {
            return new StaffEditViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Role = user.Role,
                CoordinatorUnit = user.CoordinatorUnit,
                IsHeadCoachAccount = user.Role == UserRole.HeadCoach,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                Groups = user.Groups,
                Permissions = new UpdatePermissionsDto
                {
                    CanPublishPlaybook = user.CanPublishPlaybook,
                    CanEditDepthChart = user.CanEditDepthChart,
                    CanRecordAttendance = user.CanRecordAttendance
                }
            };
        }
    }
}
