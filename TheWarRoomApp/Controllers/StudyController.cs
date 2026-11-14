using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Receives what the study timer on film and playbook pages reports, and passes it on to the API.
    // The browser never talks to the API itself.
    public class StudyController : WarRoomController
    {
        private readonly HttpClient _client;

        public StudyController(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        // POST: Study/Report   (sent by view-tracker.js every 15 seconds while a player is viewing)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report([FromForm] StudyReportDto report)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();   // 400 Bad Request
            }

            var response = await _client.PostAsJsonAsync("api/ViewingSession", report);
            if (response.IsSuccessStatusCode)   // 200 OK
            {
                // How much was counted goes back to the page, which shows it next to the timer
                var credit = await response.Content.ReadFromJsonAsync<StudyCreditDto>();
                return Json(credit);
            }

            // The script only needs to know the report was not counted (401 signed out, 403 not a player, 404, 409)
            return StatusCode((int)response.StatusCode);
        }
    }
}
