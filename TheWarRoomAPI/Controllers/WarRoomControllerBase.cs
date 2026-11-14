using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.Filters;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    // Shared by every API controller: gives access to the user the request is acting as
    public abstract class WarRoomControllerBase : ControllerBase
    {
        // Set by CurrentUserFilter before the action runs
        protected CurrentUser CurrentUser => (CurrentUser)HttpContext.Items[CurrentUserFilter.ItemKey]!;

        // 403 Forbidden with a message.
        // (The built-in Forbid() needs an authentication scheme, and this API has none.)
        protected ObjectResult Forbidden(string message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, message);
        }
    }
}
