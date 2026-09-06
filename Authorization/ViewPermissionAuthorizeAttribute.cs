using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GymSaaS.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ViewPermissionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string? _folderName;

    public ViewPermissionAuthorizeAttribute()
    {
    }

    public ViewPermissionAuthorizeAttribute(string folderName)
    {
        _folderName = folderName;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (user.IsInRole("SuperAdmin"))
            return;

        var folderName = _folderName;

        if (string.IsNullOrWhiteSpace(folderName) &&
            context.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            folderName = controllerAction.ControllerName;
        }

        if (string.IsNullOrWhiteSpace(folderName))
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissionCode = "views." + folderName.Trim().ToLowerInvariant();

        if (!user.HasClaim("AllowedView", permissionCode))
            context.Result = new ForbidResult();
    }
}
