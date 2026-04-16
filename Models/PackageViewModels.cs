using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    // ─────────────────────────────────────────────
    // Package Catalog (PackageDefinitions)
    // ─────────────────────────────────────────────

    public class PackageDefinitionListItem
    {
        public Guid PackageDefinitionId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PackageTypeCode { get; set; } = string.Empty;
        public string PackageTypeName { get; set; } = string.Empty;
        public string BranchAccessPolicy { get; set; } = string.Empty;
        public int? SessionCount { get; set; }
        public int? DurationDays { get; set; }
        public int? OpenGymDurationDays { get; set; }
        public bool IsActive { get; set; }
        public int AssignedCount { get; set; }

        // Display helpers
        public string SessionLabel => PackageTypeCode switch
        {
            "SESSION" => $"{SessionCount} sessions",
            "OPEN_GYM" => $"{DurationDays} days",
            "COMBINED" => $"{SessionCount} sessions + {OpenGymDurationDays ?? DurationDays} days gym",
            _ => "—"
        };
    }

    public class PackageDefinitionFormViewModel
    {
        public Guid? PackageDefinitionId { get; set; }

        [Required(ErrorMessage = "Package name is required")]
        [MaxLength(200)]
        public string PackageName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Package type is required")]
        public string PackageTypeCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Branch access policy is required")]
        public Guid BranchAccessPolicyTypeId { get; set; }

        // SESSION + COMBINED: number of sessions
        [Range(1, 9999, ErrorMessage = "Must be between 1 and 9999")]
        public int? SessionCount { get; set; }

        // SESSION + COMBINED: how many days the sessions are valid
        [Range(1, 3650, ErrorMessage = "Must be between 1 and 3650 days")]
        public int? DurationDays { get; set; }

        // OPEN_GYM: how many days of open gym access
        [Range(1, 3650)]
        public int? OpenGymDurationDays { get; set; }

        // COMBINED only: separate open gym duration
        // If null on COMBINED, falls back to DurationDays
        public int? OpenGymDurationDaysSeparate { get; set; }

        public int OpenGymDailyLimit { get; set; } = 1;

        public bool AllowCarryOverSessions { get; set; } = false;
        public bool AllowQueuedRenewal { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        // For dropdowns
        public List<BranchPolicyDropdownItem> BranchPolicies { get; set; } = new();

        public bool IsEdit => PackageDefinitionId.HasValue;

        // Helpers for view logic
        public bool HasSessions => PackageTypeCode is "SESSION" or "COMBINED";
        public bool HasOpenGym => PackageTypeCode is "OPEN_GYM" or "COMBINED";
        public bool IsCombined => PackageTypeCode == "COMBINED";
    }

    public class BranchPolicyDropdownItem
    {
        public Guid BranchAccessPolicyTypeId { get; set; }
        public string PolicyCode { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
    }

    // ─────────────────────────────────────────────
    // Member Package Assignment
    // ─────────────────────────────────────────────

    public class MemberPackageListItem
    {
        public Guid MemberPackageId { get; set; }
        public Guid? LinkedPackageGroupId { get; set; }
        public string PackageNameSnapshot { get; set; } = string.Empty;
        public string PackageTypeCode { get; set; } = string.Empty;
        public string? PackageComponentRole { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsCustomPackage { get; set; }
        public DateOnly ValidFromDate { get; set; }
        public DateOnly? ValidToDate { get; set; }
        public int? SessionCountOriginal { get; set; }
        public int? SessionCountRemaining { get; set; }
        public int CrossBranchVisitsUsed { get; set; }
        public int? CrossBranchVisitLimit { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        // Display helpers
        public bool IsActive => Status == "ACTIVE";
        public bool IsExpired => ValidToDate.HasValue && ValidToDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
        public bool IsExpiringSoon =>
            ValidToDate.HasValue &&
            ValidToDate.Value >= DateOnly.FromDateTime(DateTime.UtcNow) &&
            ValidToDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        // PackageName alias so views can use .PackageName or .PackageNameSnapshot interchangeably
        public string PackageName => PackageNameSnapshot;

        // Component role helpers
        public bool IsSessionBased =>
            PackageComponentRole == "SESSION" || PackageTypeCode == "SESSION";

        public bool IsOpenGym =>
            PackageComponentRole == "OPEN_GYM" || PackageTypeCode == "OPEN_GYM";

        public string ComponentLabel => PackageComponentRole switch
        {
            "SESSION" => "Sessions",
            "OPEN_GYM" => "Open Gym",
            _ => PackageTypeCode switch
            {
                "SESSION" => "Sessions",
                "OPEN_GYM" => "Open Gym",
                _ => ""
            }
        };

        public string SessionDisplay =>
            SessionCountRemaining.HasValue
                ? $"{SessionCountRemaining}/{SessionCountOriginal}"
                : "—";
    }

    // Grouped view for combined packages
    public class MemberPackageGroup
    {
        public Guid? LinkedGroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public List<MemberPackageListItem> Components { get; set; } = new();
        public bool IsCombined => Components.Count > 1;
        public bool AnyActive => Components.Any(c => c.IsActive);
    }

    public class AssignPackageViewModel
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MembershipNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a package")]
        public Guid? PackageDefinitionId { get; set; }

        // Override dates (optional — if null, calculated from package definition)
        public DateOnly? CustomStartDate { get; set; }
        public DateOnly? CustomSessionExpiry { get; set; }
        public DateOnly? CustomOpenGymExpiry { get; set; }

        // Override session count (optional)
        public int? CustomSessionCount { get; set; }

        // Carry over from previous package
        public int CarryOverSessions { get; set; } = 0;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // For dropdowns
        public List<PackageDefinitionListItem> AvailablePackages { get; set; } = new();

        // Currently active packages (to show carry-over option)
        public List<MemberPackageListItem> CurrentPackages { get; set; } = new();
    }
}