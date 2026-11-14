using System.Globalization;
using Microsoft.AspNetCore.Html;
using TheWarRoomApp.Models;

namespace TheWarRoomApp.Infrastructure
{
    // How enum values, ranks and dates are worded on the pages
    public static class DisplayText
    {
        public static string Unit(TeamUnit unit)
        {
            return unit switch
            {
                TeamUnit.Offence => "Offence",
                TeamUnit.Defence => "Defence",
                TeamUnit.SpecialTeams => "Special teams",
                _ => unit.ToString()
            };
        }

        public static string Role(UserRole role, TeamUnit? coordinatorUnit)
        {
            return role switch
            {
                UserRole.HeadCoach => "Head coach",
                UserRole.Coordinator => coordinatorUnit != null ? $"{Unit(coordinatorUnit.Value)} coordinator" : "Coordinator",
                UserRole.PositionCoach => "Position coach",
                _ => "Player"
            };
        }

        public static string Status(RosterStatus status)
        {
            return status.ToString();
        }

        public static string Event(EventType type)
        {
            return type.ToString();
        }

        public static string Attendance(AttendanceStatus status)
        {
            return status.ToString();
        }

        // The Bootstrap colour used for each attendance status
        public static string AttendanceColour(AttendanceStatus status)
        {
            return status switch
            {
                AttendanceStatus.Present => "success",
                AttendanceStatus.Late => "warning",
                AttendanceStatus.Absent => "danger",
                _ => "secondary"
            };
        }

        // "Tue, Nov 10, 2026"
        public static string Date(DateOnly date)
        {
            return date.ToString("ddd, MMM d, yyyy");
        }

        // "3:30 PM", or "3:30 PM to 5:30 PM" when the event has an end time
        public static string Times(TimeOnly start, TimeOnly? end)
        {
            return end != null ? $"{start:h:mm tt} to {end:h:mm tt}" : $"{start:h:mm tt}";
        }

        // 1 -> "1st", 2 -> "2nd", 3 -> "3rd", 4 -> "4th", 11 -> "11th"
        public static string Ordinal(int number)
        {
            int lastTwoDigits = number % 100;
            if (lastTwoDigits >= 11 && lastTwoDigits <= 13)
            {
                return $"{number}th";
            }

            return (number % 10) switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            };
        }

        public static string WeekOf(DepthChartWeekDto week)
        {
            string text = $"Week of {week.WeekStartDate:MMM d, yyyy}";
            return string.IsNullOrWhiteSpace(week.Label) ? text : $"{text} ({week.Label})";
        }

        // 71 -> 5'11"
        public static string Height(int? inches)
        {
            if (inches == null)
            {
                return string.Empty;
            }

            return $"{inches / 12}'{inches % 12}\"";
        }

        // "#7 Jalen Carter", or just the name when the player has no number yet
        public static string Player(int? jerseyNumber, string firstName, string lastName)
        {
            return jerseyNumber != null ? $"#{jerseyNumber} {firstName} {lastName}" : $"{firstName} {lastName}";
        }

        // "3 plays", "1 play"
        public static string Count(int number, string singular, string plural)
        {
            return number == 1 ? $"1 {singular}" : $"{number} {plural}";
        }

        // A moment in time. The API sends it as UTC; the page's script (site.js) rewrites it in the
        // viewer's own time zone, and the text written here is what shows until it has.
        public static IHtmlContent When(DateTime utc)
        {
            var moment = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return new HtmlString($"<time datetime=\"{moment:yyyy-MM-ddTHH:mm:ssZ}\" data-local>{moment.ToLocalTime():MMM d, yyyy h:mm tt}</time>");
        }

        // ---------- Playbook changes ----------

        // "New play", "Change to formation", "Delete play"
        public static string Change(PlaybookChangeAction action, PlaybookTargetType target)
        {
            string thing = target == PlaybookTargetType.Formation ? "formation" : "play";
            return action switch
            {
                PlaybookChangeAction.Create => $"New {thing}",
                PlaybookChangeAction.Update => $"Change to {thing}",
                _ => $"Delete {thing}"
            };
        }

        // A change that a coach with the publish right made is shown as published, not as approved by someone
        public static string Status(ApprovalRequestDto request)
        {
            return request.Status switch
            {
                ApprovalStatus.Pending => "Waiting for approval",
                ApprovalStatus.Approved => request.AutoApproved ? "Published" : "Approved",
                ApprovalStatus.Rejected => "Rejected",
                _ => "Withdrawn"
            };
        }

        // The Bootstrap colour used for each status
        public static string StatusColour(ApprovalStatus status)
        {
            return status switch
            {
                ApprovalStatus.Pending => "warning",
                ApprovalStatus.Approved => "success",
                ApprovalStatus.Rejected => "danger",
                _ => "secondary"
            };
        }

        // ---------- Film ----------

        // "Riverside Raiders at Lakeshore Lions"
        public static string Matchup(GameDto game)
        {
            return $"{game.AwayTeamName} at {game.HomeTeamName}";
        }

        // "1st & 10"
        public static string DownAndDistance(PlayTagDto tag)
        {
            return $"{Ordinal(tag.Down)} & {tag.Distance}";
        }

        public static string VideoState(VideoStatus status)
        {
            return status switch
            {
                VideoStatus.Uploading => "Uploading",
                VideoStatus.Processing => "Being checked",
                VideoStatus.Ready => "Ready",
                _ => "Could not be used"
            };
        }

        public static string CutupState(CutupStatus status)
        {
            return status switch
            {
                CutupStatus.Draft => "Draft",
                CutupStatus.Queued => "Waiting to be made into a video",
                CutupStatus.Rendering => "Being made into a video",
                CutupStatus.Ready => "Video ready",
                _ => "The video could not be made"
            };
        }

        // The Bootstrap colour used for each cutup status
        public static string CutupStateColour(CutupStatus status)
        {
            return status switch
            {
                CutupStatus.Draft => "secondary",
                CutupStatus.Ready => "success",
                CutupStatus.Failed => "danger",
                _ => "info"
            };
        }

        // "Week of Nov 16, 2026"
        public static string WeekOf(DateOnly monday)
        {
            return $"Week of {monday:MMM d, yyyy}";
        }

        // ---------- Tendencies ----------

        // "Short (1 to 3 yards)"
        public static string Distance(DistanceRange range)
        {
            return $"{DistanceName(range)} ({DistanceYards(range)})";
        }

        // The yards a range covers, in words
        public static string DistanceYards(DistanceRange range)
        {
            return range switch
            {
                DistanceRange.Short => "1 to 3 yards",
                DistanceRange.Medium => "4 to 6 yards",
                DistanceRange.Long => "7 to 10 yards",
                _ => "11 yards or more"
            };
        }

        // Just the name of the range, for a column heading or a chart label
        public static string DistanceName(DistanceRange range)
        {
            return range switch
            {
                DistanceRange.Short => "Short",
                DistanceRange.Medium => "Medium",
                DistanceRange.Long => "Long",
                _ => "Very long"
            };
        }

        // "60.0%": always one decimal, and always with a point whatever the server's language is
        public static string Percent(decimal value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        // The situation in words: "1st down", "3rd down, long (7 to 10 yards)", "every down and distance"
        public static string Situation(int? down, DistanceRange? distance)
        {
            if (down == null && distance == null)
            {
                return "every down and distance";
            }
            if (distance == null)
            {
                return $"{Ordinal(down!.Value)} down";
            }
            if (down == null)
            {
                return $"every down, {Distance(distance.Value).ToLowerInvariant()}";
            }

            return $"{Ordinal(down.Value)} down, {Distance(distance.Value).ToLowerInvariant()}";
        }

        // ---------- Study time ----------

        // A length of study time in words: a dash for none, "under 1 min", "25 min", "1 h 5 min"
        public static string StudyTime(int seconds)
        {
            if (seconds <= 0)
            {
                return "–";
            }
            if (seconds < 60)
            {
                return "under 1 min";
            }

            int minutes = seconds / 60;
            return minutes >= 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} min";
        }

        // What a coach's changes to a playbook lead to, shown next to the playbook's name
        public static string PlaybookRights(PlaybookDto playbook)
        {
            if (!playbook.CanChange)
            {
                return "You can read this playbook.";
            }

            return playbook.CanPublish
                ? "Your changes go live as soon as you save them."
                : "Your changes are sent to the head coach for approval.";
        }
    }
}
