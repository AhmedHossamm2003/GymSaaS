using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymSaaS.Models;

public class UserListItemViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public Guid RoleId { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SelectListItem> Roles { get; set; } = new();
    public List<SelectListItem> Branches { get; set; } = new();
}

public class EditUserViewModel : CreateUserViewModel
{
    public Guid UserId { get; set; }

    [DataType(DataType.Password)]
    public new string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password")]
    public new string? ConfirmPassword { get; set; }
}