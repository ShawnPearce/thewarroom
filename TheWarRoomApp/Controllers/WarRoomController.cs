using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Controllers
{
    // Shared by every controller in the client: turns the API answers that mean the same thing
    // on every page into the matching MVC result.
    public abstract class WarRoomController : Controller
    {
        private const string TemporaryPasswordKey = "TemporaryPassword";

        // Call this when an API response is not a success and the action has no special handling for it
        protected async Task<IActionResult> ApiFailureAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)   // 401: the API no longer accepts this sign-in
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)   // 403: signed in, but not allowed to do this
            {
                TempData["AccessDeniedMessage"] = await response.Content.ReadAsStringAsync();
                return RedirectToAction("AccessDenied", "Account");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)   // 404
            {
                return NotFound();
            }

            // Anything else is unexpected
            return StatusCode(StatusCodes.Status502BadGateway, "The War Room API returned an unexpected answer.");
        }

        // For actions that finish by redirecting (a button on a list, for example): the API's refusal
        // is kept for the next page, where the layout shows it as an alert.
        // Returns true when the response was a 409 Conflict or a 403 Forbidden and the message was stored.
        protected async Task<bool> StoreRefusalMessageAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.Forbidden)
            {
                TempData["Error"] = await response.Content.ReadAsStringAsync();
                return true;
            }

            return false;
        }

        // A temporary password is shown exactly once, on the page the browser is redirected to next
        protected void KeepTemporaryPassword(TemporaryPasswordDto temporaryPassword)
        {
            TempData[TemporaryPasswordKey] = JsonSerializer.Serialize(temporaryPassword);
        }

        protected TemporaryPasswordDto? TakeTemporaryPassword()
        {
            if (TempData[TemporaryPasswordKey] is string json)
            {
                return JsonSerializer.Deserialize<TemporaryPasswordDto>(json);
            }

            return null;
        }
    }
}
