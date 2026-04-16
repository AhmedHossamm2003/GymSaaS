using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("OverrideRequestStatuses", Schema = "attendance")]
[Index("StatusCode", Name = "UQ_OverrideRequestStatuses_Code", IsUnique = true)]
public partial class OverrideRequestStatus
{
    [Key]
    public Guid OverrideRequestStatusId { get; set; }

    [StringLength(30)]
    public string StatusCode { get; set; } = null!;

    [StringLength(100)]
    public string StatusName { get; set; } = null!;

    [InverseProperty("OverrideRequestStatus")]
    public virtual ICollection<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; } = new List<AttendanceOverrideRequest>();
}
