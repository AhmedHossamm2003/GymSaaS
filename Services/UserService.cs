using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GymSaaS.Services;

public class UserService : IUserService
{
    private readonly GymDbContext _db;

    public UserService(GymDbContext db)
    {
        _db = db;
    }

    public async Task<List<UserListItemViewModel>> GetAllAsync(Guid tenantId)
    {
        var users = await (
            from u in _db.Users
            join ur in _db.UserRoles on u.UserId equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.RoleId
            join ub in _db.UserBranches.Where(x => x.IsActive) on u.UserId equals ub.UserId into ubj
            from ub in ubj.DefaultIfEmpty()
            join b in _db.Branches on ub.BranchId equals b.BranchId into bj
            from b in bj.DefaultIfEmpty()
            where u.TenantId == tenantId && u.DeletedAtUtc == null
            select new UserListItemViewModel
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = r.RoleName,
                BranchName = b != null ? b.BranchName : null,
                IsActive = u.IsActive
            }
        )
        .OrderBy(x => x.FullName)
        .ToListAsync();

        return users;
    }

    public async Task<CreateUserViewModel> BuildCreateModelAsync(Guid tenantId)
    {
        var roles = await _db.Roles
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.RoleName)
            .Select(r => new SelectListItem
            {
                Value = r.RoleId.ToString(),
                Text = r.RoleName
            })
            .ToListAsync();

        var branches = await _db.Branches
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.BranchName)
            .Select(b => new SelectListItem
            {
                Value = b.BranchId.ToString(),
                Text = b.BranchName
            })
            .ToListAsync();

        return new CreateUserViewModel
        {
            IsActive = true,
            Roles = roles,
            Branches = branches
        };
    }

    public async Task<(bool Success, string? Error)> CreateAsync(Guid tenantId, CreateUserViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToUpperInvariant();

        var emailExists = await _db.Users.AnyAsync(u =>
            u.TenantId == tenantId &&
            u.DeletedAtUtc == null &&
            u.NormalizedEmail == normalizedEmail);

        if (emailExists)
            return (false, "A user with this email already exists.");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleId == model.RoleId &&
                r.TenantId == tenantId &&
                !r.IsDeleted &&
                r.IsActive);

        if (role == null)
            return (false, "Selected role was not found.");

        var isSuperAdmin = string.Equals(role.RoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && model.BranchId == null)
            return (false, "Branch is required for non-SuperAdmin users.");

        Branch? branch = null;

        if (!isSuperAdmin)
        {
            branch = await _db.Branches
                .FirstOrDefaultAsync(b =>
                    b.BranchId == model.BranchId &&
                    b.TenantId == tenantId &&
                    b.IsActive);

            if (branch == null)
                return (false, "Selected branch was not found.");
        }

        var (firstName, lastName) = SplitFullName(model.FullName);
        var (passwordHash, passwordSalt) = HashPassword(model.Password);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Email = model.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            IsActive = model.IsActive,
            IsLocked = false,
            LastLoginAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null,
            DeletedAtUtc = null
        };

        _db.Users.Add(user);

        _db.UserRoles.Add(new UserRole
        {
            UserRoleId = Guid.NewGuid(),
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAtUtc = DateTime.UtcNow
        });

        if (!isSuperAdmin && branch != null)
        {
            _db.UserBranches.Add(new UserBranch
            {
                UserBranchId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branch.BranchId,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task<EditUserViewModel?> BuildEditModelAsync(Guid userId, Guid tenantId)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == tenantId && u.DeletedAtUtc == null);

        if (user == null)
            return null;

        var currentRoleId = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .FirstOrDefaultAsync();

        var currentBranchId = await _db.UserBranches
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => (Guid?)x.BranchId)
            .FirstOrDefaultAsync();

        var model = new EditUserViewModel
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            RoleId = currentRoleId,
            BranchId = currentBranchId
        };

        model.Roles = await _db.Roles
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.RoleName)
            .Select(r => new SelectListItem
            {
                Value = r.RoleId.ToString(),
                Text = r.RoleName
            })
            .ToListAsync();

        model.Branches = await _db.Branches
            .Where(b => b.TenantId == tenantId && b.IsActive)
            .OrderBy(b => b.BranchName)
            .Select(b => new SelectListItem
            {
                Value = b.BranchId.ToString(),
                Text = b.BranchName
            })
            .ToListAsync();

        return model;
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(Guid tenantId, EditUserViewModel model)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == model.UserId && u.TenantId == tenantId && u.DeletedAtUtc == null);

        if (user == null)
            return (false, "User not found.");

        var normalizedEmail = model.Email.Trim().ToUpperInvariant();

        var duplicateEmail = await _db.Users.AnyAsync(u =>
            u.TenantId == tenantId &&
            u.DeletedAtUtc == null &&
            u.UserId != model.UserId &&
            u.NormalizedEmail == normalizedEmail);

        if (duplicateEmail)
            return (false, "Another user already uses this email.");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r =>
                r.RoleId == model.RoleId &&
                r.TenantId == tenantId &&
                !r.IsDeleted &&
                r.IsActive);

        if (role == null)
            return (false, "Selected role was not found.");

        var isSuperAdmin = string.Equals(role.RoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin && model.BranchId == null)
            return (false, "Branch is required for non-SuperAdmin users.");

        Branch? branch = null;

        if (!isSuperAdmin)
        {
            branch = await _db.Branches
                .FirstOrDefaultAsync(b =>
                    b.BranchId == model.BranchId &&
                    b.TenantId == tenantId &&
                    b.IsActive);

            if (branch == null)
                return (false, "Selected branch was not found.");
        }

        var (firstName, lastName) = SplitFullName(model.FullName);

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = model.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        user.IsActive = model.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var (passwordHash, passwordSalt) = HashPassword(model.Password);
            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
        }

        var existingRoles = await _db.UserRoles
            .Where(x => x.UserId == user.UserId)
            .ToListAsync();

        _db.UserRoles.RemoveRange(existingRoles);

        _db.UserRoles.Add(new UserRole
        {
            UserRoleId = Guid.NewGuid(),
            UserId = user.UserId,
            RoleId = role.RoleId,
            AssignedAtUtc = DateTime.UtcNow
        });

        var existingBranches = await _db.UserBranches
            .Where(x => x.UserId == user.UserId)
            .ToListAsync();

        _db.UserBranches.RemoveRange(existingBranches);

        if (!isSuperAdmin && branch != null)
        {
            _db.UserBranches.Add(new UserBranch
            {
                UserBranchId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branch.BranchId,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return (true, null);
    }

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var parts = fullName.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return ("User", "Name");

        if (parts.Length == 1)
            return (parts[0], parts[0]);

        var firstName = parts[0];
        var lastName = string.Join(' ', parts.Skip(1));

        return (firstName, lastName);
    }

    private static (string Hash, string? Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);

        using var sha256 = SHA256.Create();
        var combined = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(combined);
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }
}