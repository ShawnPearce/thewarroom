using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Filters
{
    // Runs before every action. It reads the X-User-Id header the MVC app sent, loads that user
    // from the database and makes it available to the controller as CurrentUser.
    // The API trusts this header: signing in happens in the MVC app.
    public class CurrentUserFilter : IAsyncActionFilter
    {
        public const string ItemKey = "CurrentUser";

        // The filter is registered with this order so it runs ahead of the automatic model validation (-2000).
        // A caller who is not signed in then gets 401 rather than a 400 describing the request body.
        public const int FilterOrder = -3000;

        private readonly IAuthService _authService;

        public CurrentUserFilter(IAuthService authService)
        {
            _authService = authService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Login and team registration are called before anyone is signed in
            bool allowedWithoutSignIn = context.ActionDescriptor.EndpointMetadata
                .OfType<AllowWithoutSignInAttribute>()
                .Any();
            if (allowedWithoutSignIn)
            {
                await next();
                return;
            }

            var headers = context.HttpContext.Request.Headers;

            if (!int.TryParse(headers[ApiHeaders.UserId], out int userId))
            {
                context.Result = new UnauthorizedObjectResult("Sign in required.");   // 401 Unauthorized
                return;
            }

            string? sessionToken = headers[ApiHeaders.SessionToken];

            var currentUser = await _authService.ResolveCurrentUserAsync(userId, sessionToken);
            if (currentUser == null)
            {
                context.Result = new UnauthorizedObjectResult("This sign-in is no longer valid.");   // 401 Unauthorized
                return;
            }

            context.HttpContext.Items[ItemKey] = currentUser;
            await next();
        }
    }
}
