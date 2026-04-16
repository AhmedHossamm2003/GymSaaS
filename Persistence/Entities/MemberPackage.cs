using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("MemberPackages", Schema = "membership")]
[Index("HomeBranchId", Name = "IX_MemberPackages_HomeBranchId")]
[Index("MemberId", "Status", Name = "IX_MemberPackages_MemberId_Status")]
[Index("ValidFromDate", "ValidToDate", Name = "IX_MemberPackages_ValidDates")]
public partial class MemberPackage
{
    [Key]
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

    public Guid? LinkedPackageGroupId { get; set; }

    public int OpenGymDailyLimit { get; set; }

    [StringLength(20)]
    public string? PackageComponentRole { get; set; }

    [InverseProperty("MemberPackage")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [ForeignKey("BranchAccessPolicyTypeId")]
    [InverseProperty("MemberPackages")]
    public virtual BranchAccessPolicyType BranchAccessPolicyType { get; set; } = null!;

    [ForeignKey("CancelledByUserId")]
    [InverseProperty("MemberPackageCancelledByUsers")]
    public virtual User? CancelledByUser { get; set; }

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("MemberPackageCreatedByUsers")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("HomeBranchId")]
    [InverseProperty("MemberPackages")]
    public virtual Branch HomeBranch { get; set; } = null!;

    [InverseProperty("RenewalOfMemberPackage")]
    public virtual ICollection<MemberPackage> InverseRenewalOfMemberPackage { get; set; } = new List<MemberPackage>();

    [ForeignKey("MemberId")]
    [InverseProperty("MemberPackages")]
    public virtual Member Member { get; set; } = null!;

    [ForeignKey("PackageDefinitionId")]
    [InverseProperty("MemberPackages")]
    public virtual PackageDefinition? PackageDefinition { get; set; }

    [ForeignKey("PackageTypeId")]
    [InverseProperty("MemberPackages")]
    public virtual PackageType PackageType { get; set; } = null!;

    [ForeignKey("RenewalOfMemberPackageId")]
    [InverseProperty("InverseRenewalOfMemberPackage")]
    public virtual MemberPackage? RenewalOfMemberPackage { get; set; }

    [ForeignKey("TenantId")]
    [InverseProperty("MemberPackages")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("UpdatedByUserId")]
    [InverseProperty("MemberPackageUpdatedByUsers")]
    public virtual User? UpdatedByUser { get; set; }
}
