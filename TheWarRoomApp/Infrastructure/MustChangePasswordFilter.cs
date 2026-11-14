using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TheWarRoomApp.Infrastructure
{
    // A user who still has the temporary password from the head coach is sent to the
    // change-password page, whatever page they asked for.
    public class MustChangePasswordFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return;
            }

            if (user.FindFirstValue(WarRoomClaimTypes.MustChangePassword) != "true")
            {
                return;
            }

            // Changing the password and signing out stay reachable
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();
            if (controller == "Account" && (action == "ChangePassword" || action == "Logout"))
            {
                return;
            }

            context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
