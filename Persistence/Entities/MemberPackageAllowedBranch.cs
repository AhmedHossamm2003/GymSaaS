using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("MemberPackageAllowedBranches", Schema = "membership")]
[Index("MemberPackageId", "BranchId", Name = "UQ_MemberPackageAllowedBranches_Pkg_Branch", IsUnique = true)]
public partial class MemberPackageAllowedBranch
{
    [Key]
    public Guid AllowedBranchId { get; set; }

    public Guid MemberPackageId { get; set; }

    public Guid BranchId { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("MemberPackageAllowedBranches")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("MemberPackageId")]
    [InverseProperty("MemberPackageAllowedBranches")]
    public virtual MemberPackage MemberPackage { get; set; } = null!;
}
