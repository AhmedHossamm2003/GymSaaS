using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GymSaaS.Persistence.Entities;

[Table("ExerciseCategories", Schema = "catalog")]
public partial class ExerciseCategory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int ExerciseCategoryId { get; set; }

    [StringLength(100)]
    public string CategoryName { get; set; } = null!;

    [StringLength(50)]
    public string? IconClass { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
}
