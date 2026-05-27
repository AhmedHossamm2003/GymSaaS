using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class WorkoutTemplateListItem
    {
        public Guid WorkoutTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public byte Difficulty { get; set; }
        public string DifficultyLabel => Difficulty switch { 1 => "Beginner", 2 => "Intermediate", _ => "Advanced" };
        public string DifficultyColor => Difficulty switch { 1 => "#16a34a", 2 => "#d97706", _ => "#dc2626" };
        public int? EstimatedMinutes { get; set; }
        public int ExerciseCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class WorkoutTemplateFormViewModel
    {
        public Guid? WorkoutTemplateId { get; set; }

        [Required(ErrorMessage = "Template name is required.")]
        [StringLength(200)]
        public string TemplateName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(1, 3)]
        public byte Difficulty { get; set; } = 1;

        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
        public int? EstimatedMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        public List<WorkoutTemplateExerciseFormItem> Exercises { get; set; } = new();

        public List<ExercisePickerItem> AvailableExercises { get; set; } = new();

        public bool IsEdit => WorkoutTemplateId.HasValue;
    }

    public class WorkoutTemplateExerciseFormItem
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int? Sets { get; set; }
        public int? Reps { get; set; }
        public int? DurationSeconds { get; set; }
        public int? RestSeconds { get; set; }
        public string? Notes { get; set; }
    }

    public class ExercisePickerItem
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }
}
