using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClipController : WarRoomControllerBase
    {
        private readonly IClipService _clipService;

        public ClipController(IClipService clipService)
        {
            _clipService = clipService;
        }

        // GET: api/Clip?gameId=2&offenceTeamId=3&playType=Run&down=1   (every filter is optional)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClipDto>>> GetClips([FromQuery] ClipFilterDto filter)
        {
            try
            {
                var clips = await _clipService.GetClipsAsync(CurrentUser, filter);
                return Ok(clips);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/Clip/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ClipDto>> GetClip(int id)
        {
            try
            {
                var clip = await _clipService.GetClipAsync(CurrentUser, id);
                if (clip == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(clip);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/Clip   (a clip, with its tag when the play is tagged)
        [HttpPost]
        public async Task<ActionResult<ClipDto>> CreateClip(SaveClipDto dto)
        {
            try
            {
                var created = await _clipService.CreateAsync(CurrentUser, dto);
                if (created == null)
                {
                    return NotFound();   // 404 Not Found (the video is not on this team)
                }

                return CreatedAtAction(nameof(GetClip), new { id = created.Id }, created);   // 201 Created
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (the times do not fit the video, for example)
            }
        }

        // PUT: api/Clip/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClip(int id, SaveClipDto dto)
        {
            try
            {
                var result = await _clipService.UpdateAsync(CurrentUser, id, dto);
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
                return Conflict(ex.Message);   // 409 Conflict with error message
            }
        }

        // DELETE: api/Clip/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClip(int id)
        {
            try
            {
                var result = await _clipService.DeleteAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (the clip is used in a cutup)
            }
        }
    }
}
