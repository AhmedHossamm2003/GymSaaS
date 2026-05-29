// ================================================================
// Services/Reception/IReceptionService.cs
// ================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GymSaaS.Services.Reception
{
    // ── DTOs ──────────────────────────────────────────────────────

    /// <summary>
    /// What the reception dashboard page loads on startup.
    /// </summary>
    public class ReceptionDashboardDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public int MaxCapacity { get; set; }
        public int CurrentlyPresentCount { get; set; }
        public int TodayEntryCount { get; set; }
        public int TodayClassesCount { get; set; }
        public List<TodayClassItem> TodayClasses { get; set; } = new();

        // Derived — shown as progress bar
        public int CapacityPercent => MaxCapacity > 0
            ? Math.Min(100, (int)Math.Round((double)CurrentlyPresentCount / MaxCapacity * 100))
            : 0;
    }

    /// <summary>
    /// A single class scheduled for today, with live/upcoming status.
    /// </summary>
    public class TodayClassItem
    {
        public Guid GymClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public string TimeDisplay { get; set; } = null!;
        public string? CoachName { get; set; }
        public int? Capacity { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsLive { get; set; }
        public bool IsUpcoming { get; set; }
        public bool IsDone => !IsLive && !IsUpcoming;
    }

    /// <summary>
    /// Returned by the Scan endpoint — drives the popup modal.
    /// </summary>
    public class ScanResultDto
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }      // MEMBER_NOT_FOUND | NO_ACTIVE_PACKAGE | MEMBER_INACTIVE | ALREADY_INSIDE
        public string? ErrorMessage { get; set; }

        // Member info (shown in popup)
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = null!;
        public string MembershipNumber { get; set; } = null!;
        public string? PhotoUrl { get; set; }

        // Subscription state
        public bool HasConflict { get; set; }       // true = show choice buttons
        public bool AlreadyInsideGym { get; set; }

        // Packages available for check-in
        public List<PackageOptionDto> PackageOptions { get; set; } = new();

        // If no conflict — auto check-in was done, return the record id
        public Guid? AutoCheckedInRecordId { get; set; }
        public string? AutoCheckedInPackageName { get; set; }
    }

    /// <summary>
    /// One package option shown in the conflict popup.
    /// </summary>
    public class PackageOptionDto
    {
        public Guid MemberPackageId { get; set; }
        public string PackageName { get; set; } = null!;
        public string PackageTypeCode { get; set; } = null!;    // OPEN_GYM | CLASS | SESSION | SUBSCRIPTION | BUNDLE
        public string? SessionsRemaining { get; set; }          // "12 sessions left" or null
        public string Label { get; set; } = null!;              // Button label: "Open Gym" / "Class" / etc.
    }

    /// <summary>
    /// Posted when receptionist makes a choice in the conflict popup.
    /// </summary>
    public class MarkAttendanceRequest
    {
        public Guid MemberId { get; set; }
        public Guid BranchId { get; set; }
        public Guid SelectedMemberPackageId { get; set; }
        public Guid ReceptionistUserId { get; set; }
    }

    /// <summary>
    /// Result of marking attendance.
    /// </summary>
    public class MarkAttendanceResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? AttendanceRecordId { get; set; }
    }

    // ── Interface ─────────────────────────────────────────────────

    public interface IReceptionService
    {
        /// <summary>
        /// Load dashboard stats for a branch.
        /// Admin/SuperAdmin pass any branchId; reception users get their own.
        /// </summary>
        Task<ReceptionDashboardDto?> GetDashboardAsync(Guid branchId, Guid tenantId);

        /// <summary>
        /// Triggered when receptionist enters a member's membership number
        /// (simulating the mobile QR scan for now).
        /// Resolves the member, their active packages, and decides if there's a conflict.
        /// </summary>
        Task<ScanResultDto> ProcessScanAsync(string membershipNumber, Guid branchId, Guid tenantId);

        /// <summary>
        /// Called after receptionist confirms which package to use.
        /// Records the AttendanceRecord with PresenceUntilUtc = now + MemberPresenceWindowMinutes.
        /// </summary>
        Task<MarkAttendanceResult> MarkAttendanceAsync(MarkAttendanceRequest request, Guid tenantId);

        /// <summary>
        /// Returns all branches for the tenant (used by Admin/SuperAdmin branch selector).
        /// </summary>
        Task<List<BranchOptionDto>> GetBranchesAsync(Guid tenantId);
    }

    public class BranchOptionDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public string BranchCode { get; set; } = null!;
    }
}
