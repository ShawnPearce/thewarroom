using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Position groups, their positions, and which coaches and players belong to each group
    public class GroupsController : WarRoomController
    {
        private readonly HttpClient _client;

        public GroupsController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Groups
        public async Task<IActionResult> Index()
        {
            var response = await _client.GetAsync("api/PositionGroup");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var groups = await response.Content.ReadFromJsonAsync<List<PositionGroupDto>>();
            return View(groups);   // Index View
        }

        // GET: Groups/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/PositionGroup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 404 when the group is not on this team
            }

            var group = await response.Content.ReadFromJsonAsync<PositionGroupDto>();
            return View(group);   // Details View
        }

        // GET: Groups/Create?unit=Offence
        public IActionResult Create(TeamUnit unit = TeamUnit.Offence)
        {
            // The page is for the head coach and coordinators. The API enforces this too when the form is saved.
            if (!User.IsHeadCoach() && !User.IsInRole(nameof(UserRole.Coordinator)))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(new SavePositionGroupDto { Unit = unit });
        }

        // POST: Groups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SavePositionGroupDto group)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/PositionGroup", group);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    var created = await response.Content.ReadFromJsonAsync<PositionGroupDto>();
                    return RedirectToAction(nameof(Details), new { id = created!.Id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 duplicate name
                {
                    ModelState.AddModelError("Name", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View(group);   // Create View with errors
        }

        // GET: Groups/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var response = await _client.GetAsync($"api/PositionGroup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var group = await response.Content.ReadFromJsonAsync<PositionGroupDto>();

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group!.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            ViewData["GroupId"] = id;
            return View(new SavePositionGroupDto { Unit = group!.Unit, Name = group.Name });   // Edit View
        }

        // POST: Groups/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SavePositionGroupDto group)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/PositionGroup/{id}", group);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Details), new { id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 duplicate name
                {
                    ModelState.AddModelError("Name", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            ViewData["GroupId"] = id;
            return View(group);   // Edit View with errors
        }

        // GET: Groups/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _client.GetAsync($"api/PositionGroup/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var group = await response.Content.ReadFromJsonAsync<PositionGroupDto>();

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group!.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(group);   // Delete confirmation View
        }

        // POST: Groups/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var response = await _client.DeleteAsync($"api/PositionGroup/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The group was deleted.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 409 the group still has positions
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- Who belongs to a group ----------

        // GET: Groups/Coaches/5
        public async Task<IActionResult> Coaches(int id)
        {
            var groupResponse = await _client.GetAsync($"api/PositionGroup/{id}");
            if (!groupResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(groupResponse);
            }
            var group = (await groupResponse.Content.ReadFromJsonAsync<PositionGroupDto>())!;

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group.CanAssignCoaches)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var staffResponse = await _client.GetAsync("api/User");
            if (!staffResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(staffResponse);
            }
            var staff = (await staffResponse.Content.ReadFromJsonAsync<List<UserDto>>())!;

            var model = new GroupMembersViewModel
            {
                GroupId = group.Id,
                GroupName = group.Name,
                Unit = group.Unit,
                Kind = "Coaches",
                Choices = staff
                    .Where(c => c.IsActive)
                    .Select(c => new MemberChoice
                    {
                        Id = c.Id,
                        Name = $"{c.FirstName} {c.LastName}",
                        Detail = DisplayText.Role(c.Role, c.CoordinatorUnit),
                        IsSelected = group.Coaches.Any(g => g.Id == c.Id)
                    })
                    .ToList()
            };

            return View("Members", model);   // Members View
        }

        // POST: Groups/Coaches/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Coaches(int id, List<int> selectedIds)
        {
            var response = await _client.PutAsJsonAsync($"api/PositionGroup/{id}/coaches", new SetGroupMembersDto { UserIds = selectedIds });
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The group's coaches were saved.";
            }
            else if (!await StoreRefusalMessageAsync(response))
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Groups/Players/5
        public async Task<IActionResult> Players(int id)
        {
            var groupResponse = await _client.GetAsync($"api/PositionGroup/{id}");
            if (!groupResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(groupResponse);
            }
            var group = (await groupResponse.Content.ReadFromJsonAsync<PositionGroupDto>())!;

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var rosterResponse = await _client.GetAsync("api/Roster");
            if (!rosterResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(rosterResponse);
            }
            var roster = (await rosterResponse.Content.ReadFromJsonAsync<List<RosterEntryDto>>())!;

            var model = new GroupMembersViewModel
            {
                GroupId = group.Id,
                GroupName = group.Name,
                Unit = group.Unit,
                Kind = "Players",
                Choices = roster
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.JerseyNumber ?? 999)
                    .Select(p => new MemberChoice
                    {
                        Id = p.PlayerId,
                        Name = DisplayText.Player(p.JerseyNumber, p.FirstName, p.LastName),
                        // The player's other groups, so a coach can see who already plays elsewhere
                        Detail = string.Join(", ", p.Groups.Where(g => g.Id != group.Id).Select(g => g.Name)),
                        IsSelected = group.Players.Any(g => g.Id == p.PlayerId)
                    })
                    .ToList()
            };

            return View("Members", model);   // Members View
        }

        // POST: Groups/Players/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Players(int id, List<int> selectedIds)
        {
            var response = await _client.PutAsJsonAsync($"api/PositionGroup/{id}/players", new SetGroupMembersDto { UserIds = selectedIds });
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The group's players were saved.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 for example a player who is still on the depth chart
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- Positions ----------

        // GET: Groups/CreatePosition?groupId=5
        public async Task<IActionResult> CreatePosition(int groupId)
        {
            var response = await _client.GetAsync($"api/PositionGroup/{groupId}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var group = await response.Content.ReadFromJsonAsync<PositionGroupDto>();

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group!.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View("PositionForm", new PositionFormViewModel
            {
                GroupName = group.Name,
                Position = new SavePositionDto { PositionGroupId = groupId }
            });
        }

        // POST: Groups/CreatePosition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePosition(PositionFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PostAsJsonAsync("api/Position", model.Position);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    return RedirectToAction(nameof(Details), new { id = model.Position.PositionGroupId });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the code is already used
                {
                    ModelState.AddModelError("Position.Code", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View("PositionForm", model);   // PositionForm View with errors
        }

        // GET: Groups/EditPosition/12
        public async Task<IActionResult> EditPosition(int id)
        {
            var response = await _client.GetAsync($"api/Position/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }
            var position = (await response.Content.ReadFromJsonAsync<PositionDto>())!;

            var groupResponse = await _client.GetAsync($"api/PositionGroup/{position.PositionGroupId}");
            if (!groupResponse.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(groupResponse);
            }
            var group = (await groupResponse.Content.ReadFromJsonAsync<PositionGroupDto>())!;

            // The API says whether the current user may do this; it checks again when the form is saved
            if (!group.CanManage)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View("PositionForm", new PositionFormViewModel
            {
                Id = position.Id,
                GroupName = group.Name,
                Position = new SavePositionDto
                {
                    PositionGroupId = position.PositionGroupId,
                    Code = position.Code,
                    Name = position.Name
                }
            });
        }

        // POST: Groups/EditPosition/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPosition(int id, PositionFormViewModel model)
        {
            if (id != model.Id)   // id in URL must match the form
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/Position/{id}", model.Position);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Details), new { id = model.Position.PositionGroupId });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the code is already used
                {
                    ModelState.AddModelError("Position.Code", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View("PositionForm", model);   // PositionForm View with errors
        }

        // POST: Groups/DeletePosition/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePosition(int id, int groupId)
        {
            var response = await _client.DeleteAsync($"api/Position/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The position was deleted.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 the position is on a depth chart
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }
    }
}
