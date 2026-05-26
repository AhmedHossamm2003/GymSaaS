using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Services;

public class RoleService : IRoleService
{
    private readonly GymDbContext _db;
    private readonly IWebHostEnvironment _environment;

    private static readonly string[] ExcludedViewFolders = new[]
    {
        "Shared",
        "Home",
        "Auth"
    };

    public RoleService(GymDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public async Task<List<RoleListItemViewModel>> GetAllAsync(Guid tenantId)
    {
        await SyncViewPermissionsFromFoldersAsync();

        var roles = await _db.Roles
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => new RoleListItemViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                RoleDescription = r.RoleDescription,
                IsSystem = r.IsSystem,
                IsActive = r.IsActive,
                ViewsCount = _db.RoleViewPermissions
                    .Join(_db.ViewPermissions,
                        rvp => rvp.ViewPermissionId,
                        vp => vp.ViewPermissionId,
                        (rvp, vp) => new { rvp, vp })
                    .Count(x => x.rvp.RoleId == r.RoleId && x.vp.IsActive && x.vp.GroupName == "Views")
            })
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.RoleName)
            .ToListAsync();

        return roles;
    }

    public async Task<CreateRoleViewModel> BuildCreateModelAsync()
    {
        await SyncViewPermissionsFromFoldersAsync();

        var permissions = await _db.ViewPermissions
            .Where(x => x.IsActive && x.GroupName == "Views")
            .OrderBy(x => x.DisplayName)
            .Select(x => new ViewPermissionCheckboxViewModel
            {
                ViewPermissionId = x.ViewPermissionId,
                DisplayName = x.DisplayName,
                GroupName = x.GroupName,
                IsSelected = false
            })
            .ToListAsync();

        return new CreateRoleViewModel
        {
            IsActive = true,
            Permissions = permissions
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(Guid tenantId, CreateRoleViewModel model)
    {
        await SyncViewPermissionsFromFoldersAsync();

        var normalizedName = model.RoleName.Trim();

        if (string.Equals(normalizedName, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return (false, "SuperAdmin is a fixed system role and cannot be created manually.");

        var exists = await _db.Roles.AnyAsync(r =>
            r.TenantId == tenantId &&
            !r.IsDeleted &&
            r.RoleName == normalizedName);

        if (exists)
            return (false, "A role with the same name already exists.");

        var selectedPermissionIds = model.Permissions
            .Where(x => x.IsSelected)
            .Select(x => x.ViewPermissionId)
            .Distinct()
            .ToList();

        if (!selectedPermissionIds.Any())
            return (false, "Please select at least one view folder for the role.");

        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            TenantId = tenantId,
            RoleName = normalizedName,
            RoleDescription = string.IsNullOrWhiteSpace(model.RoleDescription) ? null : model.RoleDescription.Trim(),
            IsSystem = false,
            IsActive = model.IsActive,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null
        };

        _db.Roles.Add(role);

        foreach (var permissionId in selectedPermissionIds)
        {
            _db.RoleViewPermissions.Add(new RoleViewPermission
            {
                RoleViewPermissionId = Guid.NewGuid(),
                RoleId = role.RoleId,
                ViewPermissionId = permissionId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<EditRoleViewModel?> BuildEditModelAsync(Guid roleId, Guid tenantId)
    {
        await SyncViewPermissionsFromFoldersAsync();

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId && r.TenantId == tenantId && !r.IsDeleted);

        if (role == null)
            return null;

        var selectedPermissionIds = await _db.RoleViewPermissions
            .Where(x => x.RoleId == roleId)
            .Select(x => x.ViewPermissionId)
            .ToListAsync();

        var permissions = await _db.ViewPermissions
            .Where(x => x.IsActive && x.GroupName == "Views")
            .OrderBy(x => x.DisplayName)
            .Select(x => new ViewPermissionCheckboxViewModel
            {
                ViewPermissionId = x.ViewPermissionId,
                DisplayName = x.DisplayName,
                GroupName = x.GroupName,
                IsSelected = selectedPermissionIds.Contains(x.ViewPermissionId)
            })
            .ToListAsync();

        return new EditRoleViewModel
        {
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            RoleDescription = role.RoleDescription,
            IsSystem = role.IsSystem,
            IsActive = role.IsActive,
            Permissions = permissions
        };
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(Guid tenantId, EditRoleViewModel model)
    {
        await SyncViewPermissionsFromFoldersAsync();

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleId == model.RoleId && r.TenantId == tenantId && !r.IsDeleted);

        if (role == null)
            return (false, "Role not found.");

        if (role.IsSystem)
            return (false, "System roles cannot be edited.");

        var normalizedName = model.RoleName.Trim();

        if (string.Equals(normalizedName, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return (false, "SuperAdmin is a fixed system role.");

        var duplicateExists = await _db.Roles.AnyAsync(r =>
            r.TenantId == tenantId &&
            !r.IsDeleted &&
            r.RoleId != model.RoleId &&
            r.RoleName == normalizedName);

        if (duplicateExists)
            return (false, "Another role with the same name already exists.");

        var selectedPermissionIds = model.Permissions
            .Where(x => x.IsSelected)
            .Select(x => x.ViewPermissionId)
            .Distinct()
            .ToList();

        if (!selectedPermissionIds.Any())
            return (false, "Please select at least one view folder for the role.");

        role.RoleName = normalizedName;
        role.RoleDescription = string.IsNullOrWhiteSpace(model.RoleDescription) ? null : model.RoleDescription.Trim();
        role.IsActive = model.IsActive;
        role.UpdatedAtUtc = DateTime.UtcNow;

        var currentMappings = await _db.RoleViewPermissions
            .Where(x => x.RoleId == role.RoleId)
            .ToListAsync();

        _db.RoleViewPermissions.RemoveRange(currentMappings);

        foreach (var permissionId in selectedPermissionIds)
        {
            _db.RoleViewPermissions.Add(new RoleViewPermission
            {
                RoleViewPermissionId = Guid.NewGuid(),
                RoleId = role.RoleId,
                ViewPermissionId = permissionId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return (true, null);
    }

    private async Task SyncViewPermissionsFromFoldersAsync()
    {
        var viewsRoot = Path.Combine(_environment.ContentRootPath, "Views");

        if (!Directory.Exists(viewsRoot))
            return;

        var folderNames = Directory.GetDirectories(viewsRoot)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !ExcludedViewFolders.Contains(x!, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var existingPermissions = await _db.ViewPermissions.ToListAsync();

        foreach (var folderName in folderNames)
        {
            var permissionCode = BuildPermissionCode(folderName!);
            var route = $"/{folderName}";

            var existing = existingPermissions
                .FirstOrDefault(x => x.PermissionCode == permissionCode);

            if (existing == null)
            {
                _db.ViewPermissions.Add(new ViewPermission
                {
                    ViewPermissionId = Guid.NewGuid(),
                    PermissionCode = permissionCode,
                    DisplayName = folderName!,
                    GroupName = "Views",
                    Route = route,
                    SortOrder = 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.DisplayName = folderName!;
                existing.GroupName = "Views";
                existing.Route = route;
                existing.IsActive = true;
            }
        }

        var folderPermissionCodes = folderNames
            .Select(x => BuildPermissionCode(x!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in existingPermissions)
        {
            var isManagedFolderPermission = string.Equals(permission.GroupName, "Views", StringComparison.OrdinalIgnoreCase);

            if (isManagedFolderPermission && !folderPermissionCodes.Contains(permission.PermissionCode))
            {
                permission.IsActive = false;
            }
        }

        await _db.SaveChangesAsync();
    }

    private static string BuildPermissionCode(string folderName)
    {
        return $"views.{folderName.Trim().ToLowerInvariant()}";
    }
}