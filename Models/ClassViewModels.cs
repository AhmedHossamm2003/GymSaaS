using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class ClassListItem
    {
        public Guid GymClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CoachName { get; set; } = "—";
        public Guid? CoachId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public int DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int? Capacity { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string DayName => DayOfWeek switch
        {
            0 => "Sunday",
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            _ => "—"
        };

        public string TimeDisplay =>
            $"{StartTime.ToString("HH:mm")} – {EndTime.ToString("HH:mm")}";
    }

    public class ClassFormViewModel
    {
        public Guid? GymClassId { get; set; }

        [Required(ErrorMessage = "Class name is required.")]
        [StringLength(200)]
        public string ClassName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public Guid? CoachId { get; set; }

        [Required(ErrorMessage = "Branch is required.")]
        public Guid BranchId { get; set; }

        [Required(ErrorMessage = "Day of week is required.")]
        [Range(0, 6, ErrorMessage = "Select a valid day.")]
        public int DayOfWeek { get; set; }

        [Required(ErrorMessage = "Start time is required.")]
        public string StartTimeStr { get; set; } = "09:00";

        [Required(ErrorMessage = "End time is required.")]
        public string EndTimeStr { get; set; } = "10:00";

        [Range(1, 500, ErrorMessage = "Capacity must be between 1 and 500.")]
        public int? Capacity { get; set; }

        public IFormFile? Photo { get; set; }
        public string? ExistingPhotoUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public List<BranchDropdownItem> Branches { get; set; } = new();
        public List<CoachDropdownItem> Coaches { get; set; } = new();

        public bool IsEdit => GymClassId.HasValue;
    }

    public class ClassLookupItem
    {
        public Guid GymClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }
}
