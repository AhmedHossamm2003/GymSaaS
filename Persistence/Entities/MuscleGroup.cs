using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymSaaS.Persistence.Entities;

[Table("MuscleGroups", Schema = "catalog")]
public partial class MuscleGroup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int MuscleGroupId { get; set; }

    [StringLength(100)]
    public string MuscleGroupName { get; set; } = null!;

    [StringLength(50)]
    public string BodyPart { get; set; } = null!;

    public virtual ICollection<ExerciseMuscleGroup> ExerciseMuscleGroups { get; set; } = new List<ExerciseMuscleGroup>();
}
