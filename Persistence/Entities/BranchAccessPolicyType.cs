using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("BranchAccessPolicyTypes", Schema = "membership")]
[Index("PolicyCode", Name = "UQ_BranchAccessPolicyTypes_Code", IsUnique = true)]
public partial class BranchAccessPolicyType
{
    [Key]
    public Guid BranchAccessPolicyTypeId { get; set; }

    [StringLength(30)]
    public string PolicyCode { get; set; } = null!;

    [StringLength(100)]
    public string PolicyName { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [InverseProperty("BranchAccessPolicyType")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [InverseProperty("BranchAccessPolicyType")]
    public virtual ICollection<PackageDefinition> PackageDefinitions { get; set; } = new List<PackageDefinition>();
}
