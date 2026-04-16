using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Table("UserRoles", Schema = "identityx")]
[Index("UserId", "RoleId", Name = "UQ_UserRoles_UserRole", IsUnique = true)]
public partial class UserRole
{
    [Key]
    public Guid UserRoleId { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    [Precision(0)]
    public DateTime AssignedAtUtc { get; set; }

    [ForeignKey("RoleId")]
    [InverseProperty("UserRoles")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserRoles")]
    public virtual User User { get; set; } = null!;
}
