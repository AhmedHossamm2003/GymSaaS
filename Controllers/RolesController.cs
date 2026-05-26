using GymSaaS.Models;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GymSaaS.Controllers;

[Authorize]
public class RolesController : Controller
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    private Guid GetTenantId()
    {
        return Guid.Parse(User.FindFirstValue("TenantId")!);
    }

    // ============================
    // GET: Roles
    // ============================
    public async Task<IActionResult> Index()
    {
        var tenantId = GetTenantId();

        var roles = await _roleService.GetAllAsync(tenantId);

        return View(roles);
    }

    // ============================
    // GET: Roles/Create
    // ============================
    public async Task<IActionResult> Create()
    {
        var model = await _roleService.BuildCreateModelAsync();

        return View(model);
    }

    // ============================
    // POST: Roles/Create
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model = await _roleService.BuildCreateModelAsync();
            return View(model);
        }

        var tenantId = GetTenantId();

        var result = await _roleService.CreateAsync(tenantId, model);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            model = await _roleService.BuildCreateModelAsync();
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================
    // GET: Roles/Edit/{id}
    // ============================
    public async Task<IActionResult> Edit(Guid id)
    {
        var tenantId = GetTenantId();

        var model = await _roleService.BuildEditModelAsync(id, tenantId);

        if (model == null)
            return NotFound();

        return View(model);
    }

    // ============================
    // POST: Roles/Edit
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditRoleViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var tenantId = GetTenantId();

        var result = await _roleService.UpdateAsync(tenantId, model);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================
    // GET: Roles/Details/{id}
    // ============================
    public async Task<IActionResult> Details(Guid id)
    {
        var tenantId = GetTenantId();

        var model = await _roleService.BuildEditModelAsync(id, tenantId);

        if (model == null)
            return NotFound();

        return View(model);
    }
}