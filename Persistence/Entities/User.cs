using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Users", Schema = "identityx")]
[Index("NormalizedEmail", Name = "IX_Users_NormalizedEmail")]
[Index("TenantId", Name = "IX_Users_TenantId")]
[Index("TenantId", "NormalizedEmail", Name = "UQ_Users_Tenant_Email", IsUnique = true)]
public partial class User
{
    [Key]
    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(255)]
    public string Email { get; set; } = null!;

    [StringLength(255)]
    public string NormalizedEmail { get; set; } = null!;

    [StringLength(500)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(250)]
    public string? PasswordSalt { get; set; }

    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [StringLength(201)]
    public string? FullName { get; set; }

    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public bool IsLocked { get; set; }

    [Precision(0)]
    public DateTime? LastLoginAtUtc { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    [Precision(0)]
    public DateTime? DeletedAtUtc { get; set; }

    [InverseProperty("DecisionByUser")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [InverseProperty("ReceptionistDecisionUser")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<BranchQrcode> BranchQrcodes { get; set; } = new List<BranchQrcode>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<Member> MemberCreatedByUsers { get; set; } = new List<Member>();

    [InverseProperty("DeletedByUser")]
    public virtual ICollection<Member> MemberDeletedByUsers { get; set; } = new List<Member>();

    [InverseProperty("CancelledByUser")]
    public virtual ICollection<MemberPackage> MemberPackageCancelledByUsers { get; set; } = new List<MemberPackage>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<MemberPackage> MemberPackageCreatedByUsers { get; set; } = new List<MemberPackage>();

    [InverseProperty("UpdatedByUser")]
    public virtual ICollection<MemberPackage> MemberPackageUpdatedByUsers { get; set; } = new List<MemberPackage>();

    [InverseProperty("UpdatedByUser")]
    public virtual ICollection<Member> MemberUpdatedByUsers { get; set; } = new List<Member>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<PackageDefinition> PackageDefinitionCreatedByUsers { get; set; } = new List<PackageDefinition>();

    [InverseProperty("UpdatedByUser")]
    public virtual ICollection<PackageDefinition> PackageDefinitionUpdatedByUsers { get; set; } = new List<PackageDefinition>();

    [ForeignKey("TenantId")]
    [InverseProperty("Users")]
    public virtual Tenant Tenant { get; set; } = null!;

    [InverseProperty("User")]
    public virtual ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();

    [InverseProperty("User")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
