using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : WarRoomControllerBase
    {
        private readonly IVideoService _videoService;

        public VideoController(IVideoService videoService)
        {
            _videoService = videoService;
        }

        // GET: api/Video/5   (a video with its game and its clips in the order they happen)
        [HttpGet("{id}")]
        public async Task<ActionResult<VideoDetailDto>> GetVideo(int id)
        {
            try
            {
                var video = await _videoService.GetVideoAsync(CurrentUser, id);
                if (video == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(video);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // POST: api/Video   (starts an upload: makes the record and says where the browser sends the file)
        [HttpPost]
        public async Task<ActionResult<UploadTicketDto>> StartUpload(StartUploadDto dto)
        {
            try
            {
                var ticket = await _videoService.StartUploadAsync(CurrentUser, dto);
                if (ticket == null)
                {
                    return NotFound();   // 404 Not Found (the game is not on this team)
                }

                return CreatedAtAction(nameof(GetVideo), new { id = ticket.VideoId }, ticket);   // 201 Created
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (not a video file, too large, or this server cannot take video)
            }
        }

        // PUT: api/Video/5/file   (the file itself, as the body of the request)
        // Used when film storage is a folder on this computer. With Amazon S3 the browser sends the file to the bucket.
        [HttpPut("{id}/file")]
        [DisableRequestSizeLimit]   // the service stops reading at the largest size that is taken
        public async Task<IActionResult> ReceiveFile(int id)
        {
            try
            {
                var result = await _videoService.ReceiveFileAsync(CurrentUser, id, Request.Body, HttpContext.RequestAborted);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (too large, or the video is not waiting for a file)
            }
        }

        // POST: api/Video/5/complete   (checks the file that arrived; the video is ready afterwards)
        [HttpPost("{id}/complete")]
        public async Task<ActionResult<VideoDto>> CompleteUpload(int id)
        {
            try
            {
                var video = await _videoService.CompleteUploadAsync(CurrentUser, id, HttpContext.RequestAborted);
                if (video == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(video);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (the file cannot be used, with the reason)
            }
        }

        // GET: api/Video/5/file   (the video, to play it)
        // Answers with the file when it is kept on this computer, or sends the caller on to a signed link.
        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFile(int id)
        {
            try
            {
                var file = await _videoService.GetFileAsync(CurrentUser, id);
                if (file == null)
                {
                    return NotFound();   // 404 Not Found (also when the record has no file)
                }

                if (file.SignedLink != null)
                {
                    return Redirect(file.SignedLink);   // 302 Found
                }

                // A player asks for the parts of a video it needs, so ranges are answered (206 Partial Content)
                return PhysicalFile(file.Path!, file.ContentType, enableRangeProcessing: true);
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // DELETE: api/Video/5   (the record, its plays and its file)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVideo(int id)
        {
            try
            {
                var result = await _videoService.DeleteAsync(CurrentUser, id, HttpContext.RequestAborted);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                return NoContent();   // 204 No Content
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (its plays are used in a cutup)
            }
        }
    }
}
