using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    // ── List item ─────────────────────────────────
    public class MemberListItem
    {
        public Guid MemberId { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string HomeBranchName { get; set; } = string.Empty;
        public DateTime? LastCheckIn { get; set; }

        // All active packages — a member can have multiple
        public List<ActivePackageSummary> ActivePackages { get; set; } = new();

        // Convenience — first active session package
        public ActivePackageSummary? SessionPackage =>
            ActivePackages.FirstOrDefault(p => p.IsSessionBased);

        // Convenience — first active open gym package
        public ActivePackageSummary? OpenGymPackage =>
            ActivePackages.FirstOrDefault(p => p.IsOpenGym);

        public bool HasAnyPackage => ActivePackages.Count > 0;

        // For the sessions pill on the list — show the one expiring soonest
        public ActivePackageSummary? PrimaryPackage =>
            ActivePackages.OrderBy(p => p.ValidToDate ?? DateOnly.MaxValue).FirstOrDefault();

        public string Initials => string.Concat(
            FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0].ToString().ToUpper()));

        public string TimeAgoCheckIn
        {
            get
            {
                if (LastCheckIn == null) return "Never";
                var diff = DateTime.UtcNow - LastCheckIn.Value;
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                return LastCheckIn.Value.ToString("MMM d");
            }
        }
    }

    public class ActivePackageSummary
    {
        public Guid MemberPackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string PackageTypeCode { get; set; } = string.Empty;
        public string? ComponentRole { get; set; }
        public DateOnly ValidFromDate { get; set; }
        public DateOnly? ValidToDate { get; set; }
        public int? SessionsOriginal { get; set; }
        public int? SessionsRemaining { get; set; }
        public Guid? LinkedGroupId { get; set; }

        public bool IsSessionBased => ComponentRole == "SESSION" || PackageTypeCode == "SESSION";
        public bool IsOpenGym => ComponentRole == "OPEN_GYM" || PackageTypeCode == "OPEN_GYM";

        public bool IsExpired =>
            ValidToDate.HasValue &&
            ValidToDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);

        public bool IsExpiringSoon =>
            ValidToDate.HasValue &&
            !IsExpired &&
            ValidToDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        public string ExpiryLabel =>
            ValidToDate.HasValue
                ? (IsExpired ? $"Expired {ValidToDate.Value:MMM d}" : $"Exp {ValidToDate.Value:MMM d, yyyy}")
                : "No expiry";

        public string SessionDisplay =>
            SessionsRemaining.HasValue ? $"{SessionsRemaining}/{SessionsOriginal}" : "—";
    }

    // ── Create/Edit form ─────────────────────────
    public class MemberFormViewModel
    {
        public Guid? MemberId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Home branch is required")]
        public Guid HomeBranchId { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        [MaxLength(200)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(30)]
        public string? EmergencyContactPhone { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Password — only on create
        public string? Password { get; set; }

        // Profile image upload
        public IFormFile? ProfileImage { get; set; }
        public string? ExistingImageUrl { get; set; }

        // Populated for dropdowns
        public List<BranchDropdownItem> Branches { get; set; } = new();

        public bool IsEdit => MemberId.HasValue;
    }

    // ── Details ──────────────────────────────────
    public class MemberDetailsViewModel
    {
        public Guid MemberId { get; set; }
        public string MembershipNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string HomeBranchName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
        public DateTime? LastCheckIn { get; set; }

        // All active packages grouped
        public List<MemberPackageGroup> PackageGroups { get; set; } = new();

        // Quick convenience for stats
        public int? TotalSessionsRemaining =>
            PackageGroups
                .SelectMany(g => g.Components)
                .Where(c => c.IsActive &&
                       (c.PackageComponentRole == "SESSION" || c.PackageTypeCode == "SESSION"))
                .Sum(c => c.SessionCountRemaining);

        // Attendance summary
        public int TotalAttendance { get; set; }
        public int AttendanceThisMonth { get; set; }

        public string Initials => string.Concat(
            FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0].ToString().ToUpper()));
    }

    // ── Helpers ───────────────────────────────────
    public class BranchDropdownItem
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }

    public class StatusDropdownItem
    {
        public Guid StatusId { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
    }
}