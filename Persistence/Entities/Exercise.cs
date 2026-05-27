using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Exercises", Schema = "catalog")]
[Index("TenantId", Name = "IX_Exercises_TenantId")]
public partial class Exercise
{
    [Key]
    public Guid ExerciseId { get; set; }

    public Guid TenantId { get; set; }

    [StringLength(200)]
    public string ExerciseName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

    [StringLength(500)]
    public string? VideoUrl { get; set; }

    public byte Difficulty { get; set; }

    [StringLength(200)]
    public string? Equipment { get; set; }

    public int ExerciseCategoryId { get; set; }

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

    [ForeignKey("ExerciseCategoryId")]
    public virtual ExerciseCategory Category { get; set; } = null!;

    public virtual ICollection<ExerciseMuscleGroup> ExerciseMuscleGroups { get; set; } = new List<ExerciseMuscleGroup>();
}
