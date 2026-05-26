using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Coaches", Schema = "core")]
[Index("TenantId", Name = "IX_Coaches_TenantId")]
public partial class Coach
{
    [Key]
    public Guid CoachId { get; set; }

    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [StringLength(200)]
    public string Specialty { get; set; } = null!;

    public string? Bio { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(255)]
    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    [ForeignKey("TenantId")]
    public virtual Tenant Tenant { get; set; } = null!;

    [ForeignKey("BranchId")]
    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<GymClass> GymClasses { get; set; } = new List<GymClass>();
}
