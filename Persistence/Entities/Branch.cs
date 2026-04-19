using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Branches", Schema = "core")]
[Index("TenantId", Name = "IX_Branches_TenantId")]
[Index("TenantId", "BranchCode", Name = "UQ_Branches_Tenant_BranchCode", IsUnique = true)]
public partial class Branch
{
    [Key]
    public Guid BranchId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(50)]
    public string BranchCode { get; set; } = null!;

    [StringLength(200)]
    public string BranchName { get; set; } = null!;

    [StringLength(250)]
    public string? AddressLine1 { get; set; }

    [StringLength(250)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? StateProvince { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }

    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [StringLength(255)]
    public string? ContactEmail { get; set; }

    public bool IsActive { get; set; }

    public int CurrentQrVersion { get; set; }

    public int MemberPresenceWindowMinutes { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    [InverseProperty("Branch")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [InverseProperty("Branch")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchQrcode> BranchQrcodes { get; set; } = new List<BranchQrcode>();

    [InverseProperty("Branch")]
    public virtual ICollection<MemberPackageAllowedBranch> MemberPackageAllowedBranches { get; set; } = new List<MemberPackageAllowedBranch>();

    [InverseProperty("HomeBranch")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [InverseProperty("HomeBranch")]
    public virtual ICollection<Member> Members { get; set; } = new List<Member>();

    [ForeignKey("TenantId")]
    [InverseProperty("Branches")]
    public virtual Tenant Tenant { get; set; } = null!;

    [InverseProperty("Branch")]
    public virtual ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
}
