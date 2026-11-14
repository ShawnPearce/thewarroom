using System.Security.Claims;

namespace TheWarRoomApp.Infrastructure
{
    // Attached to the "WarRoomAPI" HttpClient. Before each call goes out, it adds the signed-in
    // user's id and session token as headers, which is how the API knows who the call is for.
    public class ApiUserHeaderHandler : DelegatingHandler
    {
        public const string UserIdHeader = "X-User-Id";
        public const string SessionTokenHeader = "X-Session-Token";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApiUserHeaderHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The user is read from the login cookie on every call; nothing about a user is stored in this handler
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    request.Headers.TryAddWithoutValidation(UserIdHeader, userId);
                }

                var sessionToken = user.FindFirstValue(WarRoomClaimTypes.SessionToken);
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    request.Headers.TryAddWithoutValidation(SessionTokenHeader, sessionToken);
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
