using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("ViewPermissions", Schema = "identityx")]
[Index("PermissionCode", Name = "UQ_ViewPermissions_Code", IsUnique = true)]
public partial class ViewPermission
{
    [Key]
    public Guid ViewPermissionId { get; set; }

    [StringLength(100)]
    public string PermissionCode { get; set; } = null!;

    [StringLength(150)]
    public string DisplayName { get; set; } = null!;

    [StringLength(100)]
    public string GroupName { get; set; } = null!;

    [StringLength(200)]
    public string? Route { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [InverseProperty("ViewPermission")]
    public virtual ICollection<RoleViewPermission> RoleViewPermissions { get; set; } = new List<RoleViewPermission>();
}
