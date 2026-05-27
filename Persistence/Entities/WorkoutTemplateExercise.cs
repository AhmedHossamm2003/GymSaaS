using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymSaaS.Persistence.Entities;

[Table("WorkoutTemplateExercises", Schema = "catalog")]
public partial class WorkoutTemplateExercise
{
    [Key]
    public Guid WorkoutTemplateExerciseId { get; set; }

    public Guid WorkoutTemplateId { get; set; }

    public Guid ExerciseId { get; set; }

    public int SortOrder { get; set; }

    public int? Sets { get; set; }

    public int? Reps { get; set; }

    public int? DurationSeconds { get; set; }

    public int? RestSeconds { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [ForeignKey("WorkoutTemplateId")]
    public virtual WorkoutTemplate WorkoutTemplate { get; set; } = null!;

    [ForeignKey("ExerciseId")]
    public virtual Exercise Exercise { get; set; } = null!;
}
