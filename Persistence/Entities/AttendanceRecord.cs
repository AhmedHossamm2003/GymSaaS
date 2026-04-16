using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("AttendanceRecords", Schema = "attendance")]
[Index("BranchId", "CheckInAtUtc", Name = "IX_AttendanceRecords_Branch_CheckIn", IsDescending = new[] { false, true })]
[Index("MemberId", "BranchId", "CheckInAtUtc", Name = "IX_AttendanceRecords_Member_Branch_CheckIn", IsDescending = new[] { false, false, true })]
[Index("BranchId", "PresenceUntilUtc", Name = "IX_AttendanceRecords_PresenceUntil")]
public partial class AttendanceRecord
{
    [Key]
    public Guid AttendanceRecordId { get; set; }

    public Guid TenantId { get; set; }

    public Guid MemberId { get; set; }

    public Guid BranchId { get; set; }

    [Column("BranchQRCodeId")]
    public Guid? BranchQrcodeId { get; set; }

    public Guid? MemberPackageId { get; set; }

    public Guid AttendanceStatusId { get; set; }

    [Precision(0)]
    public DateTime CheckInAtUtc { get; set; }

    [Precision(0)]
    public DateTime PresenceUntilUtc { get; set; }

    public bool IsCrossBranchVisit { get; set; }

    public bool SessionDeducted { get; set; }

    public int SessionsDeductedCount { get; set; }

    public bool OverrideApplied { get; set; }

    public Guid? OverrideRequestId { get; set; }

    public Guid? ReceptionistDecisionUserId { get; set; }

    [StringLength(200)]
    public string? ScanDeviceIdentifier { get; set; }

    [StringLength(64)]
    public string? ScanIpAddress { get; set; }

    [StringLength(1000)]
    public string? ScanUserAgent { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [InverseProperty("ResultingAttendanceRecord")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();

    [ForeignKey("AttendanceStatusId")]
    [InverseProperty("AttendanceRecords")]
    public virtual AttendanceStatus AttendanceStatus { get; set; } = null!;

    [ForeignKey("BranchId")]
    [InverseProperty("AttendanceRecords")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("BranchQrcodeId")]
    [InverseProperty("AttendanceRecords")]
    public virtual BranchQrcode? BranchQrcode { get; set; }

    [ForeignKey("MemberId")]
    [InverseProperty("AttendanceRecords")]
    public virtual Member Member { get; set; } = null!;

    [ForeignKey("MemberPackageId")]
    [InverseProperty("AttendanceRecords")]
    public virtual MemberPackage? MemberPackage { get; set; }

    [ForeignKey("OverrideRequestId")]
    [InverseProperty("AttendanceRecords")]
    public virtual AttendanceOverrideRequest? OverrideRequest { get; set; }

    [ForeignKey("ReceptionistDecisionUserId")]
    [InverseProperty("AttendanceRecords")]
    public virtual User? ReceptionistDecisionUser { get; set; }

    [ForeignKey("TenantId")]
    [InverseProperty("AttendanceRecords")]
    public virtual Tenant Tenant { get; set; } = null!;
}
