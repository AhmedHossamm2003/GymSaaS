using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

/// <summary>
/// A single use of a non-attendance package perk recorded at reception —
/// a personal-training (PT) session (with the coach who ran it) or an InBody scan.
/// Decrements the matching counter on the member's package.
/// </summary>
[Table("MemberPerkUsages", Schema = "membership")]
[Index("MemberId", Name = "IX_MemberPerkUsages_MemberId")]
[Index("BranchId", "UsedAtUtc", Name = "IX_MemberPerkUsages_Branch_UsedAt")]
public partial class MemberPerkUsage
{
    [Key]
    public Guid PerkUsageId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    // The package whose perk counter was decremented (may be null if untracked).
    public Guid? MemberPackageId { get; set; }

    // "PT" | "INBODY"
    [StringLength(20)]
    public string PerkType { get; set; } = null!;

    // Coach who ran the PT session (null for InBody).
    public Guid? CoachId { get; set; }

    // Attendance created by the same PT action. Null when the member was
    // already checked in and only the PT delivery needed recording.
    public Guid? AttendanceRecordId { get; set; }

    // Commission terms and earned amount frozen at the moment the PT session
    // is delivered. Null for non-PT perks or plans without commission.
    [Column(TypeName = "decimal(5, 2)")]
    public decimal? CommissionPercentSnapshot { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? CommissionAmount { get; set; }

    public Guid BranchId { get; set; }

    [Precision(0)]
    public DateTime UsedAtUtc { get; set; }

    public Guid? RecordedByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
