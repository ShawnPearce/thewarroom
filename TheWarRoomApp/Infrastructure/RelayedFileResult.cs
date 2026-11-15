using Microsoft.AspNetCore.Mvc;

namespace TheWarRoomApp.Infrastructure
{
    // Hands the API's answer to a request for a video file on to the browser as it is:
    // the part of the file that was asked for (200 or 206), or the signed link to go to (302).
    // The bytes are passed along as they arrive; the file is never held in memory.
    public class RelayedFileResult : IActionResult
    {
        private readonly HttpResponseMessage _answer;

        public RelayedFileResult(HttpResponseMessage answer)
        {
            _answer = answer;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;

            using (_answer)
            {
                response.StatusCode = (int)_answer.StatusCode;

                // What a video player needs to know about the part it was sent
                var content = _answer.Content.Headers;
                if (content.ContentType != null)
                {
                    response.ContentType = content.ContentType.ToString();
                }
                if (content.ContentLength != null)
                {
                    response.ContentLength = content.ContentLength;
                }
                if (content.ContentRange != null)
                {
                    response.Headers.ContentRange = content.ContentRange.ToString();
                }
                if (_answer.Headers.AcceptRanges.Count > 0)
                {
                    response.Headers.AcceptRanges = string.Join(", ", _answer.Headers.AcceptRanges);
                }
                if (_answer.Headers.Location != null)
                {
                    response.Headers.Location = _answer.Headers.Location.ToString();
                }

                try
                {
                    await _answer.Content.CopyToAsync(response.Body, context.HttpContext.RequestAborted);
                }
                catch (OperationCanceledException)
                {
                    // The player jumped to another place in the video and no longer wants this part
                }
                catch (IOException)
                {
                    // The same, noticed while writing to a connection the browser had closed
                }
            }
        }
    }
}
