using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("MemberInvitations", Schema = "membership")]
[Index("MemberId", Name = "IX_MemberInvitations_MemberId")]
[Index("MemberPackageId", Name = "IX_MemberInvitations_MemberPackageId")]
[Index("TenantId", "GuestPhone", Name = "IX_MemberInvitations_TenantId_GuestPhone")]
public partial class MemberInvitation
{
    [Key]
    public Guid MemberInvitationId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberPackageId { get; set; }

    [StringLength(200)]
    public string GuestName { get; set; } = null!;

    [StringLength(30)]
    public string GuestPhone { get; set; } = null!;

    // Set when the invitee is an existing member found by phone search
    public Guid? InvitedMemberId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "PENDING"; // PENDING, USED, CANCELLED

    [StringLength(500)]
    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UsedAtUtc { get; set; }

    [ForeignKey("TenantId")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("MemberId")]
    [InverseProperty("SentInvitations")]
    public virtual Member Member { get; set; } = null!;

    [ForeignKey("MemberPackageId")]
    [InverseProperty("MemberInvitations")]
    public virtual MemberPackage MemberPackage { get; set; } = null!;

    [ForeignKey("InvitedMemberId")]
    [InverseProperty("ReceivedInvitations")]
    public virtual Member? InvitedMember { get; set; }

    [ForeignKey("CreatedByUserId")]
    public virtual User? CreatedByUser { get; set; }
}
