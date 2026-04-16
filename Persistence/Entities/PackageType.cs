using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("PackageTypes", Schema = "membership")]
[Index("PackageTypeCode", Name = "UQ_PackageTypes_Code", IsUnique = true)]
public partial class PackageType
{
    [Key]
    public Guid PackageTypeId { get; set; }

    [StringLength(30)]
    public string PackageTypeCode { get; set; } = null!;

    [StringLength(100)]
    public string PackageTypeName { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [InverseProperty("PackageType")]
    public virtual ICollection<MemberPackage> MemberPackages { get; set; } = new List<MemberPackage>();

    [InverseProperty("PackageType")]
    public virtual ICollection<PackageDefinition> PackageDefinitions { get; set; } = new List<PackageDefinition>();
}
