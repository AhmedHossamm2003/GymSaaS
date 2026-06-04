using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("MemberPartnerships", Schema = "membership")]
[Index("MemberId", Name = "IX_MemberPartnerships_MemberId")]
[Index("PartnershipId", Name = "IX_MemberPartnerships_PartnershipId")]
[Index("TenantId", Name = "IX_MemberPartnerships_TenantId")]
[Index("MemberId", "PartnershipId", Name = "UQ_MemberPartnerships_Member_Partnership", IsUnique = true)]
public partial class MemberPartnership
{
    [Key]
    public Guid MemberPartnershipId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid PartnershipId { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [ForeignKey("MemberId")]
    [InverseProperty("MemberPartnerships")]
    public virtual Member Member { get; set; } = null!;

    [ForeignKey("PartnershipId")]
    [InverseProperty("MemberPartnerships")]
    public virtual Partnership Partnership { get; set; } = null!;
}
