using Microsoft.AspNetCore.Mvc;
using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Services;

namespace TheWarRoomAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameController : WarRoomControllerBase
    {
        private readonly IGameService _gameService;

        public GameController(IGameService gameService)
        {
            _gameService = gameService;
        }

        // GET: api/Game?scoutedTeamId=3   (newest first; the filter is optional)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GameDto>>> GetGames([FromQuery] int? scoutedTeamId)
        {
            try
            {
                var games = await _gameService.GetGamesAsync(CurrentUser, scoutedTeamId);
                return Ok(games);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden (players)
            }
        }

        // GET: api/Game/5   (a game with its videos)
        [HttpGet("{id}")]
        public async Task<ActionResult<GameDetailDto>> GetGame(int id)
        {
            try
            {
                var game = await _gameService.GetGameAsync(CurrentUser, id);
                if (game == null)
                {
                    return NotFound();   // 404 Not Found
                }

                return Ok(game);   // 200 OK
            }
            catch (ForbiddenException ex)
            {
                return Forbidden(ex.Message);   // 403 Forbidden
            }
        }

        // POST: api/Game
        [HttpPost]
        public async Task<ActionResult<GameDto>> CreateGame(SaveGameDto dto)
        {
            try
            {
                var created = await _gameService.CreateAsync(CurrentUser, dto);
                return CreatedAtAction(nameof(GetGame), new { id = created.Id }, created);   // 201 Created
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

        // PUT: api/Game/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGame(int id, SaveGameDto dto)
        {
            try
            {
                var result = await _gameService.UpdateAsync(CurrentUser, id, dto);
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

        // DELETE: api/Game/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            try
            {
                var result = await _gameService.DeleteAsync(CurrentUser, id);
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
                return Conflict(ex.Message);   // 409 Conflict (the game has video)
            }
        }
    }
}
