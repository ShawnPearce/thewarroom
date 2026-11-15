using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheWarRoomApp.Models
{
    // The play designer page. The same page draws a new or changed formation, a new or changed play,
    // a change that is being revised while it waits for approval, and a rejected change that is sent again.
    public class DesignerViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(60, ErrorMessage = "Name can't exceed 60 characters.")]
        public string Name { get; set; } = string.Empty;

        // The drawing. The designer script keeps it in a hidden field of the form.
        [Required(ErrorMessage = "The diagram is missing.")]
        public string DiagramJson { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Notes can't exceed 1000 characters.")]
        [Display(Name = "Coaching notes")]
        public string? Notes { get; set; }

        // The version of the formation or play the drawing was started from (empty for a new one)
        public int? Version { get; set; }

        // The name under which the browser keeps an unsaved copy of the drawing
        public string? DraftKey { get; set; }

        // What the page shows around the drawing. Filled in by the controller, never read from the form.
        [BindNever]
        [ValidateNever]
        public DesignerPage Page { get; set; } = new DesignerPage();
    }

    public class DesignerPage
    {
        public string Heading { get; set; } = string.Empty;

        // A formation has players only; a play adds routes, blocks, motion and text
        public bool IsFormation { get; set; }

        public TeamUnit Unit { get; set; }

        // False means the change is sent to the head coach instead of going live
        public bool CanPublish { get; set; }

        // True when a change that is already waiting for approval is being redrawn
        public bool IsRevision { get; set; }

        // True when a change the head coach rejected is being redrawn to be sent again.
        // The page then shows what the head coach said about it.
        public bool IsResubmission { get; set; }
        public string? Feedback { get; set; }
        public string? FeedbackFrom { get; set; }
        public DateTime? FeedbackAt { get; set; }

        // The links at the top of the page, from the playbook down to what is being drawn
        public List<PageLink> Trail { get; set; } = new List<PageLink>();

        // Where the Cancel link goes
        public string CancelUrl { get; set; } = string.Empty;

        // Changing a formation: where "Save as new formation" posts to
        public string? SaveAsNewUrl { get; set; }

        // A new formation: the playbook it goes into, and the formations it can be started from
        public int? NewFormationPlaybookId { get; set; }
        public int? StartFrom { get; set; }
        public List<FormationSummaryDto> StartingPoints { get; set; } = new List<FormationSummaryDto>();
    }

    public class PageLink
    {
        public PageLink(string text, string? url)
        {
            Text = text;
            Url = url;
        }

        public string Text { get; set; }
        public string? Url { get; set; }
    }
}
