using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("GymClasses", Schema = "core")]
[Index("TenantId", Name = "IX_GymClasses_TenantId")]
public partial class GymClass
{
    [Key]
    public Guid GymClassId { get; set; }

    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? CoachId { get; set; }

    [StringLength(200)]
    public string ClassName { get; set; } = null!;

    public string? Description { get; set; }

    // 0=Sunday, 1=Monday, ..., 6=Saturday (matches .NET DayOfWeek enum)
    public int DayOfWeek { get; set; }

    [Column(TypeName = "time")]
    public TimeOnly StartTime { get; set; }

    [Column(TypeName = "time")]
    public TimeOnly EndTime { get; set; }

    public int? Capacity { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

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

    [ForeignKey("CoachId")]
    public virtual Coach? Coach { get; set; }
}
