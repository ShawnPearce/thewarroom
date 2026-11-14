using System.Net;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // The team schedule and the attendance taken at its events
    public class ScheduleController : WarRoomController
    {
        private readonly HttpClient _client;

        public ScheduleController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // GET: Schedule?show=past&type=Practice
        public async Task<IActionResult> Index(string show = "upcoming", EventType? type = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            bool past = show == "past";

            // Upcoming means today onwards; past means up to yesterday
            string url = past
                ? $"api/ScheduledEvent?to={today.AddDays(-1):yyyy-MM-dd}"
                : $"api/ScheduledEvent?from={today:yyyy-MM-dd}";
            if (type != null)
            {
                url += $"&type={type}";
            }

            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var events = (await response.Content.ReadFromJsonAsync<List<ScheduledEventDto>>())!;
            if (past)
            {
                events.Reverse();   // the most recent past event first
            }

            return View(new ScheduleViewModel { Events = events, Show = past ? "past" : "upcoming", Type = type });   // Index View
        }

        // GET: Schedule/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var response = await _client.GetAsync($"api/ScheduledEvent/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);   // 404 when the event is not on this team
            }

            var model = new EventDetailsViewModel
            {
                Event = (await response.Content.ReadFromJsonAsync<ScheduledEventDto>())!
            };

            // A player sees what was recorded for them at this event
            if (User.IsPlayer())
            {
                var mineResponse = await _client.GetAsync("api/Attendance/my");
                if (mineResponse.IsSuccessStatusCode)
                {
                    var mine = await mineResponse.Content.ReadFromJsonAsync<List<MyAttendanceDto>>();
                    model.MyStatus = mine!.FirstOrDefault(r => r.EventId == id)?.Status;
                }
            }

            return View(model);   // Details View
        }

        // GET: Schedule/Create
        public IActionResult Create()
        {
            // The page is for the head coach and coordinators. The API enforces this too when the form is saved.
            if (!CanManageSchedule())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(new SaveScheduledEventDto
            {
                EventType = EventType.Practice,
                EventDate = DateOnly.FromDateTime(DateTime.Today),
                StartTime = new TimeOnly(15, 30),
                EndTime = new TimeOnly(17, 30)
            });
        }

        // POST: Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SaveScheduledEventDto scheduledEvent)
        {
            if (ModelState.IsValid)   // check for validation errors
            {
                var response = await _client.PostAsJsonAsync("api/ScheduledEvent", scheduledEvent);
                if (response.IsSuccessStatusCode)   // 201 Created
                {
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the end time is not after the start time
                {
                    ModelState.AddModelError("EndTime", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            return View(scheduledEvent);   // Create View with errors
        }

        // GET: Schedule/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanManageSchedule())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync($"api/ScheduledEvent/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var current = await response.Content.ReadFromJsonAsync<ScheduledEventDto>();
            ViewData["EventId"] = id;
            return View(new SaveScheduledEventDto
            {
                EventType = current!.EventType,
                Title = current.Title,
                EventDate = current.EventDate,
                StartTime = current.StartTime,
                EndTime = current.EndTime,
                Location = current.Location,
                Notes = current.Notes
            });
        }

        // POST: Schedule/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SaveScheduledEventDto scheduledEvent)
        {
            if (ModelState.IsValid)
            {
                var response = await _client.PutAsJsonAsync($"api/ScheduledEvent/{id}", scheduledEvent);
                if (response.IsSuccessStatusCode)   // 204 No Content
                {
                    return RedirectToAction(nameof(Details), new { id });
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)   // 409 the end time is not after the start time
                {
                    ModelState.AddModelError("EndTime", await response.Content.ReadAsStringAsync());
                }
                else
                {
                    return await ApiFailureAsync(response);
                }
            }

            ViewData["EventId"] = id;
            return View(scheduledEvent);   // Edit View with errors
        }

        // GET: Schedule/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanManageSchedule())
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var response = await _client.GetAsync($"api/ScheduledEvent/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var scheduledEvent = await response.Content.ReadFromJsonAsync<ScheduledEventDto>();
            return View(scheduledEvent);   // Delete confirmation View
        }

        // POST: Schedule/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var response = await _client.DeleteAsync($"api/ScheduledEvent/{id}");
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "The event was deleted.";
                return RedirectToAction(nameof(Index));
            }
            else if (await StoreRefusalMessageAsync(response))   // 403 attendance was recorded, so only the head coach may delete it
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return await ApiFailureAsync(response);
        }

        // ---------- Attendance ----------

        // GET: Schedule/Attendance/5
        public async Task<IActionResult> Attendance(int id)
        {
            // The sheet is for coaches; a player has their own page
            if (User.IsPlayer())
            {
                return RedirectToAction(nameof(MyAttendance));
            }

            var response = await _client.GetAsync($"api/Attendance/event/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var sheet = await response.Content.ReadFromJsonAsync<AttendanceSheetDto>();
            return View(sheet);   // Attendance View
        }

        // POST: Schedule/Attendance/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Attendance(int id, RecordAttendanceDto sheet, bool fillPresent = false)
        {
            // "Mark the rest present" fills in every player who has no status yet
            if (fillPresent)
            {
                foreach (var entry in sheet.Entries.Where(e => e.Status == null))
                {
                    entry.Status = AttendanceStatus.Present;
                }
            }

            var response = await _client.PutAsJsonAsync($"api/Attendance/event/{id}", sheet);
            if (response.IsSuccessStatusCode)   // 204 No Content
            {
                TempData["Message"] = "Attendance saved.";
            }
            else if (!await StoreRefusalMessageAsync(response))   // 409 tracking is off or the event is in the future; 403 not permitted
            {
                return await ApiFailureAsync(response);
            }

            return RedirectToAction(nameof(Attendance), new { id });
        }

        // GET: Schedule/MyAttendance
        public async Task<IActionResult> MyAttendance()
        {
            var response = await _client.GetAsync("api/Attendance/my");
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var records = await response.Content.ReadFromJsonAsync<List<MyAttendanceDto>>();
            return View(records);   // MyAttendance View
        }

        // GET: Schedule/Summary?from=2026-09-01&to=2026-11-30
        public async Task<IActionResult> Summary(DateOnly? from, DateOnly? to)
        {
            if (User.IsPlayer())
            {
                return RedirectToAction(nameof(MyAttendance));
            }

            var filters = new List<string>();
            if (from != null)
            {
                filters.Add($"from={from:yyyy-MM-dd}");
            }
            if (to != null)
            {
                filters.Add($"to={to:yyyy-MM-dd}");
            }
            string url = "api/Attendance/summary" + (filters.Count > 0 ? "?" + string.Join("&", filters) : string.Empty);

            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return await ApiFailureAsync(response);
            }

            var rows = await response.Content.ReadFromJsonAsync<List<AttendanceSummaryDto>>();
            return View(new AttendanceSummaryViewModel { Rows = rows!, From = from, To = to });   // Summary View
        }

        // The menus and forms for changing the schedule are shown to the head coach and coordinators
        private bool CanManageSchedule()
        {
            return User.IsHeadCoach() || User.IsInRole(nameof(UserRole.Coordinator));
        }
    }
}
