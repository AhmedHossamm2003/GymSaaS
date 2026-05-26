using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("RoleViewPermissions", Schema = "identityx")]
[Index("RoleId", "ViewPermissionId", Name = "UQ_RoleViewPermissions_Role_View", IsUnique = true)]
public partial class RoleViewPermission
{
    [Key]
    public Guid RoleViewPermissionId { get; set; }

    public Guid RoleId { get; set; }

    public Guid ViewPermissionId { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    [ForeignKey("RoleId")]
    [InverseProperty("RoleViewPermissions")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("ViewPermissionId")]
    [InverseProperty("RoleViewPermissions")]
    public virtual ViewPermission ViewPermission { get; set; } = null!;
}
