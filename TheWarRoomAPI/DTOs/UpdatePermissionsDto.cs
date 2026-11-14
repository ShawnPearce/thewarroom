namespace TheWarRoomAPI.DTOs
{
    // The three switches the head coach controls for each coach
    public class UpdatePermissionsDto
    {
        public bool CanPublishPlaybook { get; set; }
        public bool CanEditDepthChart { get; set; }
        public bool CanRecordAttendance { get; set; }
    }
}
