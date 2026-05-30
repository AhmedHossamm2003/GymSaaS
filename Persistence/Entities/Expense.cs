using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Expenses", Schema = "finance")]
[Index("TenantId", "ExpenseDate", Name = "IX_Expenses_TenantId_ExpenseDate")]
[Index("BranchId", Name = "IX_Expenses_BranchId")]
[Index("CategoryCode", Name = "IX_Expenses_CategoryCode")]
public partial class Expense
{
    [Key]
    public Guid ExpenseId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    [StringLength(40)]
    public string CategoryCode { get; set; } = null!;
    // RENT / SALARIES / UTILITIES / EQUIPMENT / MARKETING / MAINTENANCE / INSURANCE / TAXES / OTHER

    [StringLength(200)]
    public string? VendorName { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(40)]
    public string? PaymentMethod { get; set; } // CASH / CARD / BANK / OTHER

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
