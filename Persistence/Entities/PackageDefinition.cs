using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("PackageDefinitions", Schema = "membership")]
[Index("TenantId", "PackageCode", Name = "UQ_PackageDefinitions_Tenant_Code", IsUnique = true)]
public partial class PackageDefinition
{
    [Key]
    public Guid PackageDefinitionId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(50)]
    public string PackageCode { get; set; } = null!;

    [StringLength(200)]
    public string PackageName { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid PackageTypeId { get; set; }

    public Guid BranchAccessPolicyTypeId { get; set; }

    public int? SessionCount { get; set; }

    public int? DurationDays { get; set; }

    public int? CrossBranchVisitLimit { get; set; }

    public int? DailyAttendanceLimit { get; set; }

    public int? WeeklyAttendanceLimit { get; set; }

    public int? MonthlyAttendanceLimit { get; set; }

    public bool AllowCarryOverSessions { get; set; }

    public bool AllowQueuedRenewal { get; set; }

    public bool AllowCustomOverrideDuringAssignment { get; set; }

    public bool IsCustomTemplate { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public int? OpenGymDurationDays { get; set; }

    public int OpenGymDailyLimit { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? Price { get; set; }

    [ForeignKey("BranchAccessPolicyTypeId")]
    [InverseProperty("PackageDefinitions")]
    public virtual BranchAccessPolicyType BranchAccessPolicyType { get; set; } = null!;

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("PackageDefinitionCreatedByUsers")]
    public virtual User? CreatedByUser { get; set; }

    [InverseProperty("PackageDefinition")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [ForeignKey("PackageTypeId")]
    [InverseProperty("PackageDefinitions")]
    public virtual PackageType PackageType { get; set; } = null!;

    [ForeignKey("TenantId")]
    [InverseProperty("PackageDefinitions")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("UpdatedByUserId")]
    [InverseProperty("PackageDefinitionUpdatedByUsers")]
    public virtual User? UpdatedByUser { get; set; }
}
