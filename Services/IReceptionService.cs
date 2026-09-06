// ================================================================
// Services/Reception/IReceptionService.cs
// ================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GymSaaS.Models;

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
        public string? PhoneNumber { get; set; }
        public string? ActivePackageName { get; set; }
        public string? ActivePackageExpiry { get; set; }

        // Class info (for CLASS-package check-ins)
        public Guid? TargetGymClassId { get; set; }
        public string? TargetClassName { get; set; }
        public string? TargetClassTime { get; set; }
        public string? TargetCoachName { get; set; }
        public int? TargetClassCapacity { get; set; }
        public int? TargetClassAttendees { get; set; }
        public bool ClassIsFull { get; set; }

        // Subscription state
        public bool HasConflict { get; set; }       // true = show choice buttons
        public bool AlreadyInsideGym { get; set; }

        // Packages available for check-in
        public List<PackageOptionDto> PackageOptions { get; set; } = new();

        // If no conflict — auto check-in was done, return the record id
        public Guid? AutoCheckedInRecordId { get; set; }
        public string? AutoCheckedInPackageName { get; set; }

        // Standalone PT plans available for this visit.
        public List<PtPackageOptionDto> PtPackageOptions { get; set; } = new();

        // InBody scans remaining.
        public int? InBodyRemaining { get; set; }
        public Guid? InBodyPackageId { get; set; }       // package holding the InBody perk

        // Active coaches at the branch — for the PT-session coach picker.
        public List<CoachOptionDto> CoachOptions { get; set; } = new();
    }

    /// <summary>One coach selectable in the PT-session picker.</summary>
    public class CoachOptionDto
    {
        public Guid CoachId { get; set; }
        public string Name { get; set; } = null!;
    }

    /// <summary>A PT balance that reception can consume for this visit.</summary>
    public class PtPackageOptionDto
    {
        public Guid MemberPackageId { get; set; }
        public string PackageName { get; set; } = null!;
        public string SourceLabel { get; set; } = null!;
        public int SessionsRemaining { get; set; }
        // Client owner for this plan. Reception may still choose a different
        // coach for the session being recorded.
        public Guid? AssignedCoachId { get; set; }
    }

    /// <summary>Result of recording a PT session or InBody scan.</summary>
    public class PerkUsageResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? Remaining { get; set; }
        public Guid? AttendanceRecordId { get; set; }
        public decimal? CommissionAmount { get; set; }
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
        public bool OverrideClassCapacity { get; set; }
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

    /// <summary>
    /// Returned by the polling endpoint — carries member info for the auto-popup.
    /// </summary>
    public class LatestCheckInDto
    {
        public Guid AttendanceRecordId { get; set; }
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = null!;
        public string MembershipNumber { get; set; } = null!;
        public string? PhotoUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PackageName { get; set; }
        public int? SessionsRemaining { get; set; }
        public string CheckInAtUtc { get; set; } = null!;

        // True when this is a mobile QR scan awaiting the receptionist's choice
        // between a class/session package and an open-gym package.
        public bool RequiresChoice { get; set; }

        // Package options to choose from when RequiresChoice is true.
        public List<PackageOptionDto> PackageOptions { get; set; } = new();
    }

    /// <summary>
    /// Result of finalizing a pending (mobile-scan) attendance record.
    /// </summary>
    public class ConfirmPendingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? SessionsRemaining { get; set; }
        public string? PackageName { get; set; }
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
        /// Returns the most recent check-in at the branch that occurred AFTER sinceUtc.
        /// Used by the reception page to poll for mobile check-ins and show auto-popup.
        /// </summary>
        Task<LatestCheckInDto?> GetLatestCheckInAsync(Guid branchId, Guid tenantId, DateTime sinceUtc);

        /// <summary>
        /// Finalizes a PENDING mobile-scan attendance record once the receptionist
        /// picks which package (class vs open gym) the member is attending under.
        /// Deducts a session if the chosen package is session-based.
        /// </summary>
        Task<ConfirmPendingResult> ConfirmPendingAsync(
            Guid attendanceRecordId, Guid selectedMemberPackageId, Guid receptionistUserId, Guid tenantId);

        /// <summary>
        /// Records a PT visit atomically: consumes the selected PT balance, logs
        /// the coach and per-session commission, and checks the member in when
        /// they are not already present.
        /// </summary>
        Task<PerkUsageResult> RecordPtSessionAsync(
            Guid memberId, Guid memberPackageId, Guid coachId, Guid branchId,
            Guid receptionistUserId, Guid tenantId);

        /// <summary>
        /// Records an InBody scan for a member: decrements InBodyRemaining on the chosen package.
        /// </summary>
        Task<PerkUsageResult> RecordInBodyAsync(
            Guid memberId, Guid memberPackageId, Guid branchId,
            Guid receptionistUserId, Guid tenantId);

        /// <summary>
        /// Returns all branches for the tenant (used by Admin/SuperAdmin branch selector).
        /// </summary>
        Task<List<BranchOptionDto>> GetBranchesAsync(Guid tenantId);

        Task<DropInPageViewModel?> BuildDropInPageAsync(
            Guid branchId, Guid tenantId, string? phone, bool canEditPrices);

        Task<DropInCheckoutResult> CheckoutDropInAsync(
            DropInCheckoutViewModel request, Guid receptionistUserId, Guid tenantId);

        Task<(bool Success, string? Error)> VoidDropInAsync(
            Guid incomeEntryId, Guid branchId, string reason, Guid userId, Guid tenantId);

        Task<(bool Success, string? Error)> UpdateDropInPricesAsync(
            DropInPriceSettingsViewModel model, Guid tenantId);
    }

    public class BranchOptionDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public string BranchCode { get; set; } = null!;
    }
}
