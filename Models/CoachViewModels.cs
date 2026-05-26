using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class CoachListItem
    {
        public Guid CoachId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Specialty { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public bool IsActive { get; set; }
        public int ClassCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string Initials => string.Concat(
            FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0].ToString().ToUpper()));
    }

    public class CoachFormViewModel
    {
        public Guid? CoachId { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialty is required.")]
        [StringLength(200)]
        public string Specialty { get; set; } = string.Empty;

        public string? Bio { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(255)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Branch is required.")]
        public Guid BranchId { get; set; }

        public IFormFile? Photo { get; set; }
        public string? ExistingPhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public List<BranchDropdownItem> Branches { get; set; } = new();
        public bool IsEdit => CoachId.HasValue;
    }

    public class CoachDropdownItem
    {
        public Guid CoachId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
    }
}
