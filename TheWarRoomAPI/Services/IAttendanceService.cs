using TheWarRoomAPI.DTOs;

namespace TheWarRoomAPI.Services
{
    // Attendance at scheduled events.
    // It can be recorded only by permitted coaches, and only while the head coach has tracking switched on.
    public interface IAttendanceService
    {
        // The sheet of one event, for coaches. Returns null when the event is not on the current user's team.
        Task<AttendanceSheetDto?> GetSheetAsync(CurrentUser currentUser, int eventId);

        // Saves a sheet. Returns false when the event is not on the current user's team.
        Task<bool> RecordAsync(CurrentUser currentUser, int eventId, RecordAttendanceDto dto);

        // The signed-in user's own records, newest first
        Task<List<MyAttendanceDto>> GetMyAttendanceAsync(CurrentUser currentUser);

        // Counts per player over a period, for coaches. Both dates are optional.
        Task<List<AttendanceSummaryDto>> GetSummaryAsync(CurrentUser currentUser, DateOnly? from, DateOnly? to);
    }
}
