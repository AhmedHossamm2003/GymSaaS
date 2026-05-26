using GymSaaS.Models;

namespace GymSaaS.Services;

public interface IUserService
{
    Task<List<UserListItemViewModel>> GetAllAsync(Guid tenantId);
    Task<CreateUserViewModel> BuildCreateModelAsync(Guid tenantId);
    Task<(bool Success, string? Error)> CreateAsync(Guid tenantId, CreateUserViewModel model);
    Task<EditUserViewModel?> BuildEditModelAsync(Guid userId, Guid tenantId);
    Task<(bool Success, string? Error)> UpdateAsync(Guid tenantId, EditUserViewModel model);
}