using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Keyless]
public partial class VwActiveMemberPackage
{
    public Guid MemberPackageId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid? PackageDefinitionId { get; set; }

    [StringLength(200)]
    public string PackageNameSnapshot { get; set; } = null!;

    public Guid PackageTypeId { get; set; }

    public Guid BranchAccessPolicyTypeId { get; set; }

    public Guid HomeBranchId { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = null!;

    public bool IsCustomPackage { get; set; }

    public int? SessionCountOriginal { get; set; }

    public int? SessionCountRemaining { get; set; }

    public int CarryOverSessionsAdded { get; set; }

    public int? DurationDays { get; set; }

    public int? CrossBranchVisitLimit { get; set; }

    public int CrossBranchVisitsUsed { get; set; }

    public int? DailyAttendanceLimit { get; set; }

    public int? WeeklyAttendanceLimit { get; set; }

    public int? MonthlyAttendanceLimit { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    [Precision(0)]
    public DateTime? ActivationDateUtc { get; set; }

    public DateOnly? QueuedStartDate { get; set; }

    public int? QueueOrder { get; set; }

    public Guid? RenewalOfMemberPackageId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? CancelledAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }
}
