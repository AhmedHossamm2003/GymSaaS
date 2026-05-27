using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    // ── Quick Media Edit ─────────────────────────────────────────────
    public class QuickMediaEditItem
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string? VideoUrl { get; set; }
        public string? PhotoUrl { get; set; }

        public string? YoutubeThumbnail
        {
            get
            {
                if (string.IsNullOrEmpty(VideoUrl)) return null;
                string? id = null;
                if (VideoUrl.Contains("youtu.be/"))
                    id = VideoUrl.Split("youtu.be/").Last().Split('?').First().Trim();
                else if (VideoUrl.Contains("v="))
                    id = VideoUrl.Split("v=").Last().Split('&').First().Trim();
                else if (VideoUrl.Contains("/embed/"))
                    id = VideoUrl.Split("/embed/").Last().Split('?').First().Trim();
                return id != null ? $"https://img.youtube.com/vi/{id}/default.jpg" : null;
            }
        }
    }


    public class ExerciseListItem
    {
        public Guid ExerciseId { get; set; }
        public string ExerciseName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryIconClass { get; set; }
        public byte Difficulty { get; set; }
        public string DifficultyLabel => Difficulty switch { 1 => "Beginner", 2 => "Intermediate", _ => "Advanced" };
        public string DifficultyColor => Difficulty switch { 1 => "#16a34a", 2 => "#d97706", _ => "#dc2626" };
        public string? Equipment { get; set; }
        public string? PhotoUrl { get; set; }
        public string? VideoUrl { get; set; }
        public List<string> MuscleGroupNames { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string? YoutubeVideoId
        {
            get
            {
                if (string.IsNullOrEmpty(VideoUrl)) return null;
                if (VideoUrl.Contains("youtu.be/"))
                    return VideoUrl.Split("youtu.be/").Last().Split('?').First().Trim();
                if (VideoUrl.Contains("v="))
                    return VideoUrl.Split("v=").Last().Split('&').First().Trim();
                if (VideoUrl.Contains("/embed/"))
                    return VideoUrl.Split("/embed/").Last().Split('?').First().Trim();
                return null;
            }
        }

        public bool IsVimeo => !string.IsNullOrEmpty(VideoUrl) && VideoUrl.Contains("vimeo.com");

        public string? VideoThumbnailUrl =>
            YoutubeVideoId != null ? $"https://img.youtube.com/vi/{YoutubeVideoId}/mqdefault.jpg" : null;

        public string? EmbedUrl
        {
            get
            {
                if (YoutubeVideoId != null)
                    return $"https://www.youtube.com/embed/{YoutubeVideoId}?autoplay=1";
                if (IsVimeo)
                {
                    var id = VideoUrl!.TrimEnd('/').Split('/').Last();
                    return $"https://player.vimeo.com/video/{id}?autoplay=1";
                }
                return null;
            }
        }

        public bool HasVideo => EmbedUrl != null;
    }

    public class ExerciseFormViewModel
    {
        public Guid? ExerciseId { get; set; }

        [Required(ErrorMessage = "Exercise name is required.")]
        [StringLength(200)]
        public string ExerciseName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Instructions { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        public int ExerciseCategoryId { get; set; }

        [Range(1, 3)]
        public byte Difficulty { get; set; } = 1;

        [StringLength(200)]
        public string? Equipment { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Enter a valid URL.")]
        public string? VideoUrl { get; set; }

        public IFormFile? Photo { get; set; }
        public string? ExistingPhotoUrl { get; set; }

        public List<int> SelectedMuscleGroupIds { get; set; } = new();

        public bool IsActive { get; set; } = true;

        public List<ExerciseCategoryDropdownItem> Categories { get; set; } = new();
        public List<MuscleGroupItem> AllMuscleGroups { get; set; } = new();

        public bool IsEdit => ExerciseId.HasValue;
    }

    public class ExerciseCategoryDropdownItem
    {
        public int ExerciseCategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? IconClass { get; set; }
    }

    public class MuscleGroupItem
    {
        public int MuscleGroupId { get; set; }
        public string MuscleGroupName { get; set; } = string.Empty;
        public string BodyPart { get; set; } = string.Empty;
    }
}
