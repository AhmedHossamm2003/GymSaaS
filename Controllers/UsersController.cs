using GymSaaS.Models;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymSaaS.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    private Guid GetTenantId()
    {
        return Guid.Parse(User.FindFirstValue("TenantId")!);
    }

    // ============================
    // GET: Users
    // ============================
    public async Task<IActionResult> Index()
    {
        var tenantId = GetTenantId();
        var users = await _userService.GetAllAsync(tenantId);
        return View(users);
    }

    // ============================
    // GET: Users/Create
    // ============================
    public async Task<IActionResult> Create()
    {
        var tenantId = GetTenantId();
        var model = await _userService.BuildCreateModelAsync(tenantId);
        return View(model);
    }

    // ============================
    // POST: Users/Create
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        var tenantId = GetTenantId();

        if (!ModelState.IsValid)
        {
            var freshModel = await _userService.BuildCreateModelAsync(tenantId);
            freshModel.FullName = model.FullName;
            freshModel.Email = model.Email;
            freshModel.PhoneNumber = model.PhoneNumber;
            freshModel.RoleId = model.RoleId;
            freshModel.BranchId = model.BranchId;
            freshModel.IsActive = model.IsActive;

            return View(freshModel);
        }

        var result = await _userService.CreateAsync(tenantId, model);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);

            var freshModel = await _userService.BuildCreateModelAsync(tenantId);
            freshModel.FullName = model.FullName;
            freshModel.Email = model.Email;
            freshModel.PhoneNumber = model.PhoneNumber;
            freshModel.RoleId = model.RoleId;
            freshModel.BranchId = model.BranchId;
            freshModel.IsActive = model.IsActive;

            return View(freshModel);
        }

        TempData["Toast"] = "User created successfully.";
        TempData["ToastType"] = "success";

        return RedirectToAction(nameof(Index));
    }

    // ============================
    // GET: Users/Edit/{id}
    // ============================
    public async Task<IActionResult> Edit(Guid id)
    {
        var tenantId = GetTenantId();
        var model = await _userService.BuildEditModelAsync(id, tenantId);

        if (model == null)
            return NotFound();

        return View(model);
    }

    // ============================
    // POST: Users/Edit
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        var tenantId = GetTenantId();

        if (!ModelState.IsValid)
        {
            var freshModel = await _userService.BuildEditModelAsync(model.UserId, tenantId);
            if (freshModel == null)
                return NotFound();

            freshModel.FullName = model.FullName;
            freshModel.Email = model.Email;
            freshModel.PhoneNumber = model.PhoneNumber;
            freshModel.RoleId = model.RoleId;
            freshModel.BranchId = model.BranchId;
            freshModel.IsActive = model.IsActive;
            freshModel.Password = model.Password;
            freshModel.ConfirmPassword = model.ConfirmPassword;

            return View(freshModel);
        }

        var result = await _userService.UpdateAsync(tenantId, model);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);

            var freshModel = await _userService.BuildEditModelAsync(model.UserId, tenantId);
            if (freshModel == null)
                return NotFound();

            freshModel.FullName = model.FullName;
            freshModel.Email = model.Email;
            freshModel.PhoneNumber = model.PhoneNumber;
            freshModel.RoleId = model.RoleId;
            freshModel.BranchId = model.BranchId;
            freshModel.IsActive = model.IsActive;
            freshModel.Password = model.Password;
            freshModel.ConfirmPassword = model.ConfirmPassword;

            return View(freshModel);
        }

        TempData["Toast"] = "User updated successfully.";
        TempData["ToastType"] = "success";

        return RedirectToAction(nameof(Index));
    }

    // ============================
    // GET: Users/Details/{id}
    // ============================
    public async Task<IActionResult> Details(Guid id)
    {
        var tenantId = GetTenantId();
        var model = await _userService.BuildEditModelAsync(id, tenantId);

        if (model == null)
            return NotFound();

        return View(model);
    }
}