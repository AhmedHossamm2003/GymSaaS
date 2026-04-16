using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Tenants", Schema = "core")]
[Index("TenantCode", Name = "UQ_Tenants_TenantCode", IsUnique = true)]
public partial class Tenant
{
    [Key]
    public Guid TenantId { get; set; }

    [StringLength(50)]
    public string TenantCode { get; set; } = null!;

    [StringLength(200)]
    public string TenantName { get; set; } = null!;

    [StringLength(250)]
    public string? LegalName { get; set; }

    [StringLength(255)]
    public string? ContactEmail { get; set; }

    [StringLength(30)]
    public string? ContactPhone { get; set; }

    [StringLength(10)]
    public string DefaultLanguage { get; set; } = null!;

    [StringLength(100)]
    public string TimeZoneId { get; set; } = null!;

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    [InverseProperty("Tenant")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [InverseProperty("Tenant")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("Tenant")]
    public virtual ICollection<BranchQrcode> BranchQrcodes { get; set; } = new List<BranchQrcode>();

    [InverseProperty("Tenant")]
    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();

    [InverseProperty("Tenant")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [InverseProperty("Tenant")]
    public virtual ICollection<Member> Members { get; set; } = new List<Member>();

    [InverseProperty("Tenant")]
    public virtual ICollection<PackageDefinition> PackageDefinitions { get; set; } = new List<PackageDefinition>();

    [InverseProperty("Tenant")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
