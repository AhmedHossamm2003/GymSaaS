using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("BranchQRCodes", Schema = "attendance")]
[Index("QrCodeValue", Name = "UQ_BranchQRCodes_QrCodeValue", IsUnique = true)]
public partial class BranchQrcode
{
    [Key]
    [Column("BranchQRCodeId")]
    public Guid BranchQrcodeId { get; set; }

    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    [StringLength(500)]
    public string QrCodeValue { get; set; } = null!;

    [StringLength(2000)]
    public string SignedPayload { get; set; } = null!;

    public int VersionNo { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime EffectiveFromUtc { get; set; }

    [Precision(0)]
    public DateTime? EffectiveToUtc { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    [InverseProperty("BranchQrcode")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [InverseProperty("BranchQrcode")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [ForeignKey("BranchId")]
    [InverseProperty("BranchQrcodes")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("CreatedByUserId")]
    [InverseProperty("BranchQrcodes")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("TenantId")]
    [InverseProperty("BranchQrcodes")]
    public virtual Tenant Tenant { get; set; } = null!;
}
