using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace TheWarRoomApp.Controllers
{
    // Shared by every controller in the client: turns the API answers that mean the same thing
    // on every page into the matching MVC result.
    public abstract class WarRoomController : Controller
    {
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
    }
}
