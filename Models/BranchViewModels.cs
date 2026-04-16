using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    public class BranchListItem
    {
        public Guid BranchId { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool HasQrCode { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int MemberCount { get; set; }
        public int TodayCheckins { get; set; }
        public int InsideNow { get; set; }
    }

    public class BranchDetailsViewModel
    {
        public Guid BranchId { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? StateProvince { get; set; }
        public string? Country { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public bool IsActive { get; set; }
        public int MemberPresenceWindowMinutes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int MemberCount { get; set; }
        public int TodayCheckins { get; set; }
        public int InsideNow { get; set; }
        public string? QrCodeValue { get; set; }
        public int? QrVersionNo { get; set; }
        public DateTime? QrGeneratedAt { get; set; }
        public bool QrIsActive { get; set; }
        public bool HasQrCode => !string.IsNullOrEmpty(QrCodeValue) && QrIsActive;

        public string FullAddress
        {
            get
            {
                var parts = new[] { AddressLine1, AddressLine2, City, StateProvince, Country }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                return string.Join(", ", parts);
            }
        }
    }

    public class BranchFormViewModel
    {
        public Guid? BranchId { get; set; }
        public string BranchCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Branch name is required")]
        [MaxLength(200)]
        public string BranchName { get; set; } = string.Empty;

        [MaxLength(250)] public string? AddressLine1 { get; set; }
        [MaxLength(250)] public string? AddressLine2 { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(100)] public string? StateProvince { get; set; }
        [MaxLength(100)] public string? Country { get; set; }
        [MaxLength(30)] public string? ContactPhone { get; set; }

        [MaxLength(255)]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string? ContactEmail { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(15, 480, ErrorMessage = "Must be between 15 and 480 minutes")]
        public int MemberPresenceWindowMinutes { get; set; } = 120;

        public bool IsEdit => BranchId.HasValue;
    }
}