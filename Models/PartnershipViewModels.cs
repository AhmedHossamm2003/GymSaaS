using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class PartnershipListItem
    {
        public Guid PartnershipId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? LogoImageUrl { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public bool IsActive { get; set; }
        public int MemberCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PartnershipFormViewModel
    {
        public Guid? PartnershipId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Range(0, 100)]
        public decimal? DiscountPercentage { get; set; }

        public bool IsActive { get; set; } = true;

        public IFormFile? Logo { get; set; }
        public string? ExistingLogoUrl { get; set; }
    }

    public class MemberPartnershipItem
    {
        public Guid MemberPartnershipId { get; set; }
        public Guid PartnershipId { get; set; }
        public string PartnershipName { get; set; } = string.Empty;
        public string? LogoImageUrl { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public DateTime AssignedAtUtc { get; set; }
    }
}
