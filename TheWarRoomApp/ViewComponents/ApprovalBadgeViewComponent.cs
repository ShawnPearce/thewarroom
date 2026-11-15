using Microsoft.AspNetCore.Mvc;
using TheWarRoomApp.Infrastructure;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.ViewComponents
{
    // The number beside "Approvals" in the menu, which is how a coach is told that something needs them:
    // the head coach sees how many changes are waiting for a decision, and another coach sees how many
    // of their own changes the head coach sent back that they have not opened yet.
    // It is asked from the API each time a page is drawn, so it is right on the next click.
    // On a phone the menu is folded away, so the button that opens it carries a dot instead ("dot").
    public class ApprovalBadgeViewComponent : ViewComponent
    {
        // Where the answer is kept while one page is drawn, so the menu and its button share one call to the API
        private const string ItemKey = "ApprovalBadge";

        private readonly HttpClient _client;

        public ApprovalBadgeViewComponent(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("WarRoomAPI");
        }

        public async Task<IViewComponentResult> InvokeAsync(bool dot = false)
        {
            if (HttpContext.Items[ItemKey] is not ApprovalBadgeViewModel badge)
            {
                badge = await AskAsync();
                HttpContext.Items[ItemKey] = badge;
            }

            return View(dot ? "Dot" : "Default", badge);
        }

        private async Task<ApprovalBadgeViewModel> AskAsync()
        {
            var badge = new ApprovalBadgeViewModel();

            try
            {
                var response = await _client.GetAsync("api/ApprovalRequest/notices");
                if (response.IsSuccessStatusCode)
                {
                    var notices = (await response.Content.ReadFromJsonAsync<ApprovalNoticesDto>())!;

                    if (HttpContext.User.IsHeadCoach())
                    {
                        badge.Count = notices.WaitingForDecision;
                        badge.Meaning = notices.WaitingForDecision == 1 ? "change is waiting for your decision" : "changes are waiting for your decision";
                    }
                    else
                    {
                        badge.Count = notices.SentBack;
                        badge.Meaning = notices.SentBack == 1 ? "change was sent back to you" : "changes were sent back to you";
                    }
                }
            }
            catch (HttpRequestException)
            {
                // The API cannot be reached: the menu is drawn without the number, and the page itself says what is wrong
            }
            catch (TaskCanceledException)
            {
                // The API took too long to answer: the same
            }

            return badge;
        }
    }
}
