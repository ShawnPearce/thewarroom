using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CutupController : WarRoomControllerBase
    {
        private readonly ICutupService _cutupService;

        public CutupController(ICutupService cutupService)
        {
            _cutupService = cutupService;
        }

        // GET: api/Cutup   (a coach gets every cutup of the team; a player gets the ones shared with them)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CutupDto>>> GetCutups()
        {
            var cutups = await _cutupService.GetCutupsAsync(CurrentUser);
            return Ok(cutups);   // 200 OK
        }

        // GET: api/Cutup/5   (a cutup with its plays in order)
        [HttpGet("{id}")]
        public async Task<ActionResult<CutupDetailDto>> GetCutup(int id)
        {
            try
            {
                var cutup = await _cutupService.GetCutupAsync(CurrentUser, id);
                if (cutup == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(cutup);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (not shared with this player)
            }
        }

        // POST: api/Cutup   (a new cutup starts as a draft with no plays)
        [HttpPost]
        public async Task<ActionResult<CutupDto>> CreateCutup(SaveCutupDto dto)
        {
            try
            {
                var created = await _cutupService.CreateAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetCutup), new { id = created.Id }, created);   // 201 Created
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // PUT: api/Cutup/5   (its name, description and week)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCutup(int id, SaveCutupDto dto)
        {
            try
            {
                var result = await _cutupService.UpdateAsync(CurrentUser, id, dto);
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
        }

        // DELETE: api/Cutup/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCutup(int id)
        {
            try
            {
                var result = await _cutupService.DeleteAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (it is being made into a video right now)
            }
        }

        // POST: api/Cutup/5/clips   (adds clips to the end of the cutup, in the order given)
        [HttpPost("{id}/clips")]
        public async Task<IActionResult> AddClips(int id, AddCutupClipsDto dto)
        {
            try
            {
                var result = await _cutupService.AddClipsAsync(CurrentUser, id, dto);
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
                return Conflict(ex.Message);   // 409 Conflict (not a draft any more, or a clip from elsewhere)
            }
        }

        // POST: api/Cutup/plays/40/move   (one step earlier or later)
        [HttpPost("plays/{playId}/move")]
        public async Task<IActionResult> MovePlay(int playId, MoveCutupPlayDto dto)
        {
            try
            {
                var result = await _cutupService.MovePlayAsync(CurrentUser, playId, dto.Direction);
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
                return Conflict(ex.Message);   // 409 Conflict (not a draft any more)
            }
        }

        // DELETE: api/Cutup/plays/40   (takes the play out of its cutup; the clip itself stays)
        [HttpDelete("plays/{playId}")]
        public async Task<IActionResult> RemovePlay(int playId)
        {
            try
            {
                var result = await _cutupService.RemovePlayAsync(CurrentUser, playId);
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
                return Conflict(ex.Message);   // 409 Conflict (not a draft any more)
            }
        }

        // POST: api/Cutup/5/render   (asks for the cutup to be made into one video)
        [HttpPost("{id}/render")]
        public async Task<IActionResult> RequestRender(int id)
        {
            try
            {
                var result = await _cutupService.RequestRenderAsync(CurrentUser, id);
                if (!result)
                {
                    return NotFound();   // 404 Not Found
                }

                // The video is made in the background, in the order cutups were asked for
                return Accepted();   // 202 Accepted
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);   // 409 Conflict (no plays, a play without a video file, or already made)
            }
        }

        // POST: api/Cutup/5/draft   (back to a draft, so its plays can be changed; the video that was made is thrown away)
        [HttpPost("{id}/draft")]
        public async Task<IActionResult> ReturnToDraft(int id)
        {
            try
            {
                var result = await _cutupService.ReturnToDraftAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (it is being made into a video right now)
            }
        }

        // GET: api/Cutup/5/file   (the finished video, to play it)
        // Answers with the file when it is kept on this computer, or sends the caller on to a signed link.
        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFile(int id)
        {
            try
            {
                var file = await _cutupService.GetFileAsync(CurrentUser, id);
                if (file == null)
                {
                    return NotFound();   // 404 Not Found (also when the video has not been made yet)
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
                return Forbidden(ex.Message);   // 403 Forbidden (not shared with this player)
            }
        }

        // PUT: api/Cutup/5/sharing   (everyone the cutup is shared with after the change)
        [HttpPut("{id}/sharing")]
        public async Task<IActionResult> SetSharing(int id, SetCutupSharingDto dto)
        {
            try
            {
                var result = await _cutupService.SetSharingAsync(CurrentUser, id, dto);
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
                return Conflict(ex.Message);   // 409 Conflict (a group or player from outside the team)
            }
        }
    }
}
