namespace TheWarRoomAPI.DTOs
{
    // The three numbers of the activity report. They are measured separately and never added together:
    // being signed in is not the same as studying. They show activity, not proof of learning.
    public class StudyTimeDto
    {
        // Time signed in: from each sign-in to its last request
        public int LoginSeconds { get; set; }

        // Time a shared film was actually playing
        public int FilmSeconds { get; set; }

        // Time actively spent on playbook pages
        public int PlaybookSeconds { get; set; }

        public int SignIns { get; set; }
    }

    // One week of the team: a row for every player the current user may see
    public class ActivityReportDto
    {
        // The Monday the week starts on
        public DateOnly WeekStart { get; set; }

        public List<ActivityRowDto> Players { get; set; } = new List<ActivityRowDto>();
    }

    public class ActivityRowDto : StudyTimeDto
    {
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
    }

    // One player over the last weeks, newest week first
    public class PlayerActivityDto
    {
        public int PlayerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public List<string> Groups { get; set; } = new List<string>();

        public List<ActivityWeekDto> Weeks { get; set; } = new List<ActivityWeekDto>();
    }

    public class ActivityWeekDto : StudyTimeDto
    {
        public DateOnly WeekStart { get; set; }
    }
}
