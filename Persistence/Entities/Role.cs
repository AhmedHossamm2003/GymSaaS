using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Roles", Schema = "identityx")]
public partial class Role
{
    [Key]
    public Guid RoleId { get; set; }

    [StringLength(50)]
    public string RoleName { get; set; } = null!;

    [StringLength(255)]
    public string? RoleDescription { get; set; }

    [Precision(0)]
    public DateTime CreatedAtUtc { get; set; }

    public Guid TenantId { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAtUtc { get; set; }

    [InverseProperty("Role")]
    public virtual ICollection<RoleViewPermission> RoleViewPermissions { get; set; } = new List<RoleViewPermission>();

    [ForeignKey("TenantId")]
    [InverseProperty("Roles")]
    public virtual Tenant Tenant { get; set; } = null!;

    [InverseProperty("Role")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
