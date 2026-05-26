using GymSaaS.Models;

namespace GymSaaS.Services;

public interface IRoleService
{
    Task<List<RoleListItemViewModel>> GetAllAsync(Guid tenantId);
    Task<CreateRoleViewModel> BuildCreateModelAsync();
    Task<(bool Success, string? Error)> CreateAsync(Guid tenantId, CreateRoleViewModel model);
    Task<EditRoleViewModel?> BuildEditModelAsync(Guid roleId, Guid tenantId);
    Task<(bool Success, string? Error)> UpdateAsync(Guid tenantId, EditRoleViewModel model);
}