using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("UserBranches", Schema = "identityx")]
[Index("UserId", "BranchId", Name = "UQ_UserBranches_UserBranch", IsUnique = true)]
public partial class UserBranch
{
    [Key]
    public Guid UserBranchId { get; set; }

    public Guid UserId { get; set; }

    public Guid BranchId { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime AssignedAtUtc { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("UserBranches")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserBranches")]
    public virtual User User { get; set; } = null!;
}
