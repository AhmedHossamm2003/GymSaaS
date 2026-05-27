using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("WorkoutTemplates", Schema = "catalog")]
[Index("TenantId", Name = "IX_WorkoutTemplates_TenantId")]
public partial class WorkoutTemplate
{
    [Key]
    public Guid WorkoutTemplateId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(200)]
    public string TemplateName { get; set; } = null!;

    public string? Description { get; set; }

    public byte Difficulty { get; set; }

    public int? EstimatedMinutes { get; set; }

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

    public virtual ICollection<WorkoutTemplateExercise> WorkoutTemplateExercises { get; set; } = new List<WorkoutTemplateExercise>();
}
