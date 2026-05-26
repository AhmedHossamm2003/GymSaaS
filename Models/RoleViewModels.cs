using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models;

public class RoleListItemViewModel
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? RoleDescription { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int ViewsCount { get; set; }
}

public class ViewPermissionCheckboxViewModel
{
    public Guid ViewPermissionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class CreateRoleViewModel
{
    [Required]
    [StringLength(100)]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? RoleDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public List<ViewPermissionCheckboxViewModel> Permissions { get; set; } = new();
}

public class EditRoleViewModel : CreateRoleViewModel
{
    public Guid RoleId { get; set; }
    public bool IsSystem { get; set; }
}