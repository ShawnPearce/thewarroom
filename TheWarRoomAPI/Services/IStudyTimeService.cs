using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Counts the time players spend viewing film and playbooks
    public interface IStudyTimeService
    {
        // Adds what a page reports to the player's viewing time and says how much of it was counted.
        // Null means the playbook or cutup is not on the current user's team.
        Task<StudyCreditDto?> ReportAsync(CurrentUser currentUser, StudyReportDto dto);
    }
}
