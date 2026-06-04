using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Partnerships", Schema = "membership")]
[Index("TenantId", Name = "IX_Partnerships_TenantId")]
public partial class Partnership
{
    [Key]
    public Guid PartnershipId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? LogoImageUrl { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    [InverseProperty("Partnership")]
    public virtual ICollection<MemberPartnership> MemberPartnerships { get; set; } = new List<MemberPartnership>();
}
