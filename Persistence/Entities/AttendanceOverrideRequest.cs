using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("AttendanceOverrideRequests", Schema = "attendance")]
[Index("BranchId", "OverrideRequestStatusId", "RequestedAtUtc", Name = "IX_AttendanceOverrideRequests_Branch_Status", IsDescending = new[] { false, false, true })]
public partial class AttendanceOverrideRequest
{
    [Key]
    public Guid AttendanceOverrideRequestId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid BranchId { get; set; }

    [Column("BranchQRCodeId")]
    public Guid? BranchQrcodeId { get; set; }

    [Precision(0)]
    public DateTime RequestedAtUtc { get; set; }

    [StringLength(50)]
    public string FailureReasonCode { get; set; } = null!;

    [StringLength(300)]
    public string FailureReasonText { get; set; } = null!;

    public Guid OverrideRequestStatusId { get; set; }

    [StringLength(200)]
    public string? RequestedFromDeviceIdentifier { get; set; }

    [StringLength(64)]
    public string? RequestedFromIpAddress { get; set; }

    [StringLength(1000)]
    public string? RequestedFromUserAgent { get; set; }

    [Precision(0)]
    public DateTime? DecisionAtUtc { get; set; }

    public Guid? DecisionByUserId { get; set; }

    [StringLength(500)]
    public string? DecisionReason { get; set; }

    public Guid? ResultingAttendanceRecordId { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [InverseProperty("OverrideRequest")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [ForeignKey("BranchId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("BranchQrcodeId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual BranchQrcode? BranchQrcode { get; set; }

    [ForeignKey("DecisionByUserId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual User? DecisionByUser { get; set; }

    [ForeignKey("MemberId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual Member Member { get; set; } = null!;

    [ForeignKey("OverrideRequestStatusId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual OverrideRequestStatus OverrideRequestStatus { get; set; } = null!;

    [ForeignKey("ResultingAttendanceRecordId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual AttendanceRecord? ResultingAttendanceRecord { get; set; }

    [ForeignKey("TenantId")]
    [InverseProperty("AttendanceOverrideRequests")]
    public virtual Tenant Tenant { get; set; } = null!;
}
