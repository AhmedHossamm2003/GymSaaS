using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("ManualIncomeEntries", Schema = "finance")]
[Index("TenantId", "IncomeDate", Name = "IX_ManualIncomeEntries_TenantId_IncomeDate")]
[Index("BranchId", Name = "IX_ManualIncomeEntries_BranchId")]
[Index("CategoryCode", Name = "IX_ManualIncomeEntries_CategoryCode")]
public partial class ManualIncomeEntry
{
    [Key]
    public Guid IncomeEntryId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    [StringLength(40)]
    public string CategoryCode { get; set; } = null!;
    // DAY_PASS / MERCHANDISE / PERSONAL_TRAINING / SUPPLEMENT / OTHER

    [StringLength(200)]
    public string Description { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal Amount { get; set; }

    public DateOnly IncomeDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(40)]
    public string? PaymentMethod { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    [ForeignKey("TenantId")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("BranchId")]
    public virtual Branch? Branch { get; set; }

    [ForeignKey("CreatedByUserId")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("UpdatedByUserId")]
    public virtual User? UpdatedByUser { get; set; }
}
