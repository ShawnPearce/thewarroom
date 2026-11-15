using TheWarRoomAPI.DTOs;
using TheWarRoomAPI.Models;

namespace TheWarRoomAPI.Services
{
    // Map entity → DTO for the film records that more than one service returns
    public static class FilmMapping
    {
        // The clip must be loaded with its video, the video's game and its two teams,
        // its tag with the team on offence, and the cutup plays that use it.
        public static ClipDto ToDto(Clip clip)
        {
            var game = clip.Video!.Game!;

            return new ClipDto
            {
                Id = clip.Id,
                VideoId = clip.VideoId,
                VideoTitle = clip.Video.Title,
                GameId = game.Id,
                GameDate = game.GameDate,
                Matchup = Labels.Matchup(game),
                StartMs = clip.StartMs,
                EndMs = clip.EndMs,
                Title = clip.Title,
                Tag = ToDto(clip.Tag),
                AnnotationsJson = clip.AnnotationsJson,
                CutupCount = clip.CutupClips?.Count ?? 0
            };
        }

        public static PlayTagDto? ToDto(PlayTag? tag)
        {
            if (tag == null)
            {
                return null;
            }

            return new PlayTagDto
            {
                PlayType = tag.PlayType,
                Down = tag.Down,
                Distance = tag.Distance,
                OffenceTeamId = tag.OffenceTeamId,
                OffenceTeamName = tag.OffenceTeam?.Name ?? string.Empty
            };
        }
    }
}
