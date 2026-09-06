using System.ComponentModel.DataAnnotations;
using GymSaaS.Services.Reception;

namespace GymSaaS.Models;

public static class DropInProductCodes
{
    public const string OpenGym = "DROP_IN_OPEN_GYM";
    public const string ClassPass = "DROP_IN_CLASS_PASS";
}

public class DropInPageViewModel
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? PhoneSearch { get; set; }
    public bool SearchPerformed { get; set; }
    public DropInMemberLookup? ExistingMember { get; set; }
    public decimal OpenGymPrice { get; set; }
    public decimal ClassPassPrice { get; set; }
    public bool CanEditPrices { get; set; }
    public List<BranchOptionDto> Branches { get; set; } = new();
    public List<DropInClassOption> TodayClasses { get; set; } = new();
    public List<DropInRecentSale> RecentSales { get; set; } = new();
    public DropInCheckoutViewModel Checkout { get; set; } = new();
}

public class DropInMemberLookup
{
    public Guid MemberId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool HasOpenGymCoverage { get; set; }
    public bool HasClassCoverage { get; set; }
    public string? CoverageSummary { get; set; }
}

public class DropInClassOption
{
    public Guid GymClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string? CoachName { get; set; }
    public int? Capacity { get; set; }
    public int AttendeeCount { get; set; }
    public bool IsFull => Capacity.HasValue && AttendeeCount >= Capacity.Value;
}

public class DropInRecentSale
{
    public Guid IncomeEntryId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MembershipNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class DropInCheckoutViewModel
{
    public Guid BranchId { get; set; }
    public Guid? MemberId { get; set; }

    [Required, MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [EmailAddress, MaxLength(255)]
    public string? Email { get; set; }

    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [Required]
    public string ProductCode { get; set; } = DropInProductCodes.OpenGym;

    public Guid? GymClassId { get; set; }

    [Required, Range(0.01, 99999999)]
    public decimal? FinalPrice { get; set; }

    [Required, MaxLength(40)]
    public string PaymentMethod { get; set; } = "CASH";

    [MaxLength(500)]
    public string? PriceOverrideReason { get; set; }

    public bool ConfirmPaidDropIn { get; set; }
}

public class DropInPriceSettingsViewModel
{
    [Range(0.01, 99999999, ErrorMessage = "Open gym drop-in price must be greater than zero.")]
    public decimal OpenGymPrice { get; set; }

    [Range(0.01, 99999999, ErrorMessage = "Class pass price must be greater than zero.")]
    public decimal ClassPassPrice { get; set; }
}

public class DropInCheckoutResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? MembershipNumber { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public Guid? IncomeEntryId { get; set; }
    public decimal Amount { get; set; }
    public bool MemberCreated { get; set; }
}
