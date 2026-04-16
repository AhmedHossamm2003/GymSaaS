using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("Roles", Schema = "identityx")]
[Index("RoleName", Name = "UQ_Roles_Name", IsUnique = true)]
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

    [InverseProperty("Role")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
