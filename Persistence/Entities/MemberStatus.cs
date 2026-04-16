using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("MemberStatuses", Schema = "membership")]
[Index("StatusCode", Name = "UQ_MemberStatuses_Code", IsUnique = true)]
public partial class MemberStatus
{
    [Key]
    public Guid MemberStatusId { get; set; }

    [StringLength(30)]
    public string StatusCode { get; set; } = null!;

    [StringLength(100)]
    public string StatusName { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [InverseProperty("MemberStatus")]
    public virtual ICollection<Member> Members { get; set; } = new List<Member>();
}
