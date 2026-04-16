using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("AttendanceStatuses", Schema = "attendance")]
[Index("StatusCode", Name = "UQ_AttendanceStatuses_Code", IsUnique = true)]
public partial class AttendanceStatus
{
    [Key]
    public Guid AttendanceStatusId { get; set; }

    [StringLength(30)]
    public string StatusCode { get; set; } = null!;

    [StringLength(100)]
    public string StatusName { get; set; } = null!;

    [InverseProperty("AttendanceStatus")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
