using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Members", Schema = "membership")]
[Index("LastName", "FirstName", Name = "IX_Members_FullName")]
[Index("HomeBranchId", Name = "IX_Members_HomeBranchId")]
[Index("NormalizedEmail", Name = "IX_Members_NormalizedEmail")]
[Index("MemberStatusId", Name = "IX_Members_Status")]
[Index("TenantId", Name = "IX_Members_TenantId")]
[Index("TenantId", "NormalizedEmail", Name = "UQ_Members_Tenant_Email", IsUnique = true)]
[Index("TenantId", "MembershipNumber", Name = "UQ_Members_Tenant_MembershipNumber", IsUnique = true)]
[Index("TenantId", "PhoneNumber", Name = "UQ_Members_Tenant_Phone", IsUnique = true)]
public partial class Member
{
    [Key]
    public Guid MemberId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(50)]
    public string MembershipNumber { get; set; } = null!;

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
    public string PhoneNumber { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    public Guid HomeBranchId { get; set; }

    public Guid MemberStatusId { get; set; }

    [StringLength(1000)]
    public string? ProfileImageUrl { get; set; }

    [StringLength(200)]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime? LastLoginAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? DeletedAtUtc { get; set; }

    public Guid? DeletedByUserId { get; set; }

    [InverseProperty("Member")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [InverseProperty("Member")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("MemberCreatedByUsers")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("DeletedByUserId")]
    [InverseProperty("MemberDeletedByUsers")]
    public virtual User? DeletedByUser { get; set; }

    [ForeignKey("HomeBranchId")]
    [InverseProperty("Members")]
    public virtual Branch HomeBranch { get; set; } = null!;

    [InverseProperty("Member")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [ForeignKey("MemberStatusId")]
    [InverseProperty("Members")]
    public virtual MemberStatus MemberStatus { get; set; } = null!;

    [ForeignKey("TenantId")]
    [InverseProperty("Members")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("UpdatedByUserId")]
    [InverseProperty("MemberUpdatedByUsers")]
    public virtual User? UpdatedByUser { get; set; }
}
