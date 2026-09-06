using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class CoachListItem
    {
        public Guid CoachId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Specialty { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public bool IsActive { get; set; }
        public int ClassCount { get; set; }
        public int ActiveTraineeCount { get; set; }
        public int CoachTarget { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string Initials => string.Concat(
            FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0].ToString().ToUpper()));
    }

    public class CoachFormViewModel
    {
        public Guid? CoachId { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialty is required.")]
        [StringLength(200)]
        public string Specialty { get; set; } = string.Empty;

        public string? Bio { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(255)]
        public string? Email { get; set; }

        public Guid BranchId { get; set; }

        public List<Guid> BranchIds { get; set; } = new();

        [Range(0, 999, ErrorMessage = "Target must be between 0 and 999.")]
        public int CoachTarget { get; set; } = 0;

        // Required on create; optional on edit (blank keeps the current password).
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [DataType(DataType.Password)]
        public string? LoginPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(LoginPassword), ErrorMessage = "Passwords do not match.")]
        public string? ConfirmLoginPassword { get; set; }

        public IFormFile? Photo { get; set; }
        public string? ExistingPhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public List<BranchDropdownItem> Branches { get; set; } = new();
        public bool IsEdit => CoachId.HasValue;

        // Set on edit to show whether a User account exists
        public bool HasLinkedUser { get; set; }
        public string? LinkedUserEmail { get; set; }
    }

    public class CoachDropdownItem
    {
        public Guid CoachId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public List<Guid> BranchIds { get; set; } = new();
    }

    public class CoachDashboardViewModel
    {
        public Guid CoachId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public int ActiveTraineeCount { get; set; }
        public int CoachTarget { get; set; }
        public int TargetProgress => CoachTarget > 0 ? (int)((double)ActiveTraineeCount / CoachTarget * 100) : 0;
        public int ClassCount { get; set; }

        // Extra analytics
        public int NewTraineesThisMonth { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int SessionsRemainingTotal { get; set; }
        public int SessionsDeliveredThisMonth { get; set; }
        public int TodayClassesCount { get; set; }
        public List<CoachClassScheduleItem> TodayClasses { get; set; } = new();
        public List<CoachClassScheduleItem> AllClasses { get; set; } = new();
        public List<CoachUpcomingTraineeItem> UpcomingExpirations { get; set; } = new();

        // Earnings (commission from private training packages)
        public decimal EarningsThisMonth { get; set; }
        public decimal EarningsAllTime { get; set; }
        public int CommissionSessionsThisMonth { get; set; }
        public List<CoachEarningsItem> RecentEarnings { get; set; } = new();
    }

    public class CoachEarningsItem
    {
        public Guid MemberPackageId { get; set; }
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public decimal PackagePrice { get; set; }
        public decimal CommissionPercent { get; set; }
        public decimal CommissionAmount { get; set; }
        public DateTime EarnedAtUtc { get; set; }
    }

    public class CoachUpcomingTraineeItem
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public DateOnly ValidToDate { get; set; }
        public int DaysLeft => ValidToDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
    }

    public class CoachMyTraineeItem
    {
        public Guid MemberPackageId { get; set; }
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string TrainingType { get; set; } = string.Empty;
        public string? ClassName { get; set; }
        public int? SessionsRemaining { get; set; }
        public int? DaysRemaining { get; set; }
        public DateOnly ValidToDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CoachMyScheduleViewModel
    {
        public Guid CoachId { get; set; }
        public List<CoachClassScheduleItem> Classes { get; set; } = new();
    }

    public class CoachClassScheduleItem
    {
        public Guid GymClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int? Capacity { get; set; }
        public string DayName => DayOfWeek switch
        {
            0 => "Sunday",
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            _ => "?"
        };
    }

    public class CoachPerformanceViewModel
    {
        public Guid CoachId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int ActiveTraineeCount { get; set; }
        public int CoachTarget { get; set; }
        public int TargetProgress => CoachTarget > 0 ? (int)((double)ActiveTraineeCount / CoachTarget * 100) : 0;
        public decimal TotalRevenue { get; set; }
        public int ClassesThisMonth { get; set; }
    }
}
