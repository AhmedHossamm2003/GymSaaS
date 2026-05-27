using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymSaaS.Persistence.Entities;

[Table("ExerciseMuscleGroups", Schema = "catalog")]
public partial class ExerciseMuscleGroup
{
    public Guid ExerciseId { get; set; }

    public int MuscleGroupId { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("ExerciseId")]
    public virtual Exercise Exercise { get; set; } = null!;

    [ForeignKey("MuscleGroupId")]
    public virtual MuscleGroup MuscleGroup { get; set; } = null!;
}
