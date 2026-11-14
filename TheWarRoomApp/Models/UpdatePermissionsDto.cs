using System.ComponentModel.DataAnnotations;

namespace TheWarRoomApp.Models
{
    // The three switches the head coach controls for each coach
    public class UpdatePermissionsDto
    {
        [Display(Name = "Publish playbook changes without approval")]
        public bool CanPublishPlaybook { get; set; }

        [Display(Name = "Edit the depth chart")]
        public bool CanEditDepthChart { get; set; }

        [Display(Name = "Record attendance")]
        public bool CanRecordAttendance { get; set; }
    }
}
