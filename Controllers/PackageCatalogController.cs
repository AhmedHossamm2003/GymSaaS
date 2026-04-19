using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "AdminAndAbove")]
    public class PackageCatalogController : Controller
    {
        private readonly GymDbContext _db;

        public PackageCatalogController(GymDbContext db) => _db = db;

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /PackageCatalog
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var packages = await _db.PackageDefinitions
                .Where(p => p.TenantId == TenantId)
                .Join(_db.PackageTypes,
                      p => p.PackageTypeId,
                      pt => pt.PackageTypeId,
                      (p, pt) => new { p, pt })
                .Join(_db.BranchAccessPolicyTypes,
                      x => x.p.BranchAccessPolicyTypeId,
                      bp => bp.BranchAccessPolicyTypeId,
                      (x, bp) => new { x.p, x.pt, bp })
                .OrderBy(x => x.pt.PackageTypeCode)
                .ThenBy(x => x.p.SortOrder)
                .ThenBy(x => x.p.PackageName)
                .Select(x => new PackageDefinitionListItem
                {
                    PackageDefinitionId = x.p.PackageDefinitionId,
                    PackageCode = x.p.PackageCode,
                    PackageName = x.p.PackageName,
                    Description = x.p.Description,
                    PackageTypeCode = x.pt.PackageTypeCode,
                    PackageTypeName = x.pt.PackageTypeName,
                    BranchAccessPolicy = x.bp.PolicyCode,
                    SessionCount = x.p.SessionCount,
                    DurationDays = x.p.DurationDays,
                    OpenGymDurationDays = x.p.OpenGymDurationDays,
                    Price = x.p.Price,
                    IsActive = x.p.IsActive,
                    SortOrder = x.p.SortOrder,
                    AssignedCount = _db.MemberPackages
                                            .Count(mp => mp.PackageDefinitionId == x.p.PackageDefinitionId
                                                      && mp.Status == "ACTIVE"),
                })
                .ToListAsync();

            ViewData["Title"] = "Package Catalog";
            return View(packages);
        }

        // ─────────────────────────────────────────────
        // GET /PackageCatalog/Create
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Create()
        {
            var vm = new PackageDefinitionFormViewModel
            {
                BranchPolicies = await GetPoliciesAsync(),
            };
            ViewData["Title"] = "Package Catalog";
            ViewData["Subtitle"] = "New Package";
            return View("CreateEdit", vm);
        }

        // ─────────────────────────────────────────────
        // POST /PackageCatalog/Create
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PackageDefinitionFormViewModel model)
        {
            await ValidatePackageModel(model, null);

            if (!ModelState.IsValid)
            {
                model.BranchPolicies = await GetPoliciesAsync();
                ViewData["Title"] = "Package Catalog";
                ViewData["Subtitle"] = "New Package";
                return View("CreateEdit", model);
            }

            var typeId = await GetPackageTypeIdAsync(model.PackageTypeCode!.Trim().ToUpperInvariant());
            if (typeId == null)
            {
                ModelState.AddModelError(nameof(model.PackageTypeCode), $"Invalid package type: '{model.PackageTypeCode}'");
                model.BranchPolicies = await GetPoliciesAsync();
                ViewData["Title"] = "Package Catalog";
                ViewData["Subtitle"] = "New Package";
                return View("CreateEdit", model);
            }

            var code = await GeneratePackageCodeAsync(model.PackageName);

            var pkg = new PackageDefinition
            {
                PackageDefinitionId = Guid.NewGuid(),
                TenantId = TenantId,
                PackageCode = code,
                PackageName = model.PackageName.Trim(),
                Description = model.Description?.Trim(),
                PackageTypeId = typeId.Value,
                BranchAccessPolicyTypeId = model.BranchAccessPolicyTypeId,
                SessionCount = model.HasSessions ? model.SessionCount : null,
                DurationDays = model.DurationDays,
                OpenGymDurationDays = model.IsCombined ? model.OpenGymDurationDaysSeparate : null,
                OpenGymDailyLimit = model.OpenGymDailyLimit,
                Price = model.Price,
                AllowCarryOverSessions = model.AllowCarryOverSessions,
                AllowQueuedRenewal = model.AllowQueuedRenewal,
                AllowCustomOverrideDuringAssignment = true,
                IsCustomTemplate = false,
                IsActive = model.IsActive,
                SortOrder = model.SortOrder,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.PackageDefinitions.Add(pkg);
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Package \"{pkg.PackageName}\" created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // GET /PackageCatalog/Edit/id
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Edit(Guid id)
        {
            var pkg = await _db.PackageDefinitions
                .Include(p => p.PackageType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == id && p.TenantId == TenantId);

            if (pkg == null) return NotFound();

            var vm = new PackageDefinitionFormViewModel
            {
                PackageDefinitionId = pkg.PackageDefinitionId,
                PackageName = pkg.PackageName,
                Description = pkg.Description,
                PackageTypeCode = pkg.PackageType?.PackageTypeCode ?? "",
                BranchAccessPolicyTypeId = pkg.BranchAccessPolicyTypeId,
                SessionCount = pkg.SessionCount,
                DurationDays = pkg.DurationDays,
                OpenGymDurationDaysSeparate = pkg.OpenGymDurationDays,
                OpenGymDailyLimit = pkg.OpenGymDailyLimit,
                Price = pkg.Price,
                AllowCarryOverSessions = pkg.AllowCarryOverSessions,
                AllowQueuedRenewal = pkg.AllowQueuedRenewal,
                IsActive = pkg.IsActive,
                SortOrder = pkg.SortOrder,
                BranchPolicies = await GetPoliciesAsync(),
            };

            ViewData["Title"] = "Package Catalog";
            ViewData["Subtitle"] = "Edit Package";
            return View("CreateEdit", vm);
        }

        // ─────────────────────────────────────────────
        // POST /PackageCatalog/Edit/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, PackageDefinitionFormViewModel model)
        {
            var pkg = await _db.PackageDefinitions
                .Include(p => p.PackageType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == id && p.TenantId == TenantId);

            if (pkg == null) return NotFound();

            await ValidatePackageModel(model, id);

            if (!ModelState.IsValid)
            {
                model.BranchPolicies = await GetPoliciesAsync();
                ViewData["Title"] = "Package Catalog";
                ViewData["Subtitle"] = "Edit Package";
                return View("CreateEdit", model);
            }

            pkg.PackageName = model.PackageName.Trim();
            pkg.Description = model.Description?.Trim();
            pkg.BranchAccessPolicyTypeId = model.BranchAccessPolicyTypeId;
            pkg.SessionCount = model.HasSessions ? model.SessionCount : null;
            pkg.DurationDays = model.DurationDays;
            pkg.OpenGymDurationDays = model.IsCombined ? model.OpenGymDurationDaysSeparate : null;
            pkg.OpenGymDailyLimit = model.OpenGymDailyLimit;
            pkg.Price = model.Price;
            pkg.AllowCarryOverSessions = model.AllowCarryOverSessions;
            pkg.AllowQueuedRenewal = model.AllowQueuedRenewal;
            pkg.IsActive = model.IsActive;
            pkg.SortOrder = model.SortOrder;
            pkg.UpdatedAtUtc = DateTime.UtcNow;
            pkg.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Package \"{pkg.PackageName}\" updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // POST /PackageCatalog/ToggleActive/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(Guid id)
        {
            var pkg = await _db.PackageDefinitions
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == id && p.TenantId == TenantId);

            if (pkg == null) return NotFound();

            pkg.IsActive = !pkg.IsActive;
            pkg.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"\"{pkg.PackageName}\" {(pkg.IsActive ? "activated" : "archived")}.";
            TempData["ToastType"] = pkg.IsActive ? "success" : "warning";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────
        private async Task<List<BranchPolicyDropdownItem>> GetPoliciesAsync()
        {
            var raw = await _db.BranchAccessPolicyTypes
                .Select(bp => new BranchPolicyDropdownItem
                {
                    BranchAccessPolicyTypeId = bp.BranchAccessPolicyTypeId,
                    PolicyCode = bp.PolicyCode,
                    PolicyName = bp.PolicyName,   // raw DB value — mapped below
                })
                .ToListAsync();

            // Map display names in memory (switch expressions can't be translated to SQL)
            foreach (var item in raw)
            {
                item.PolicyName = item.PolicyCode switch
                {
                    "HOME_ONLY" => "Home Branch Only",
                    "SELECTED_BRANCHES" => "Home Branch + Limited Other Branches",
                    "ALL_BRANCHES" => "All Branches",
                    _ => item.PolicyName
                };
            }

            return raw;
        }

        private async Task<Guid?> GetPackageTypeIdAsync(string code) =>
            await _db.PackageTypes
                .Where(pt => pt.PackageTypeCode == code)
                .Select(pt => (Guid?)pt.PackageTypeId)
                .FirstOrDefaultAsync();

        private async Task<string> GeneratePackageCodeAsync(string name)
        {
            var slug = new string(name.ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(6)
                .ToArray());

            string code;
            do { code = $"PKG-{slug}-{Random.Shared.Next(100, 999)}"; }
            while (await _db.PackageDefinitions.AnyAsync(p =>
                p.TenantId == TenantId && p.PackageCode == code));

            return code;
        }

        private async Task ValidatePackageModel(PackageDefinitionFormViewModel model, Guid? excludeId)
        {
            var typeCode = (model.PackageTypeCode ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(typeCode))
                ModelState.AddModelError(nameof(model.PackageTypeCode), "Please select a package type.");
            else
            {
                var exists = await _db.PackageTypes.AnyAsync(pt => pt.PackageTypeCode == typeCode);
                if (!exists)
                    ModelState.AddModelError(nameof(model.PackageTypeCode), $"Invalid package type: '{typeCode}'");
            }

            if (string.IsNullOrWhiteSpace(model.PackageName))
                ModelState.AddModelError(nameof(model.PackageName), "Package name is required.");

            if (model.BranchAccessPolicyTypeId == Guid.Empty)
                ModelState.AddModelError(nameof(model.BranchAccessPolicyTypeId), "Please select a branch access policy.");

            if (typeCode is "SESSION" or "COMBINED")
            {
                if (model.SessionCount == null || model.SessionCount < 1)
                    ModelState.AddModelError(nameof(model.SessionCount), "Session count is required.");
            }

            if (typeCode is "SESSION" or "OPEN_GYM" or "COMBINED")
            {
                if (model.DurationDays == null || model.DurationDays < 1)
                    ModelState.AddModelError(nameof(model.DurationDays), "Duration (days) is required.");
            }

            if (typeCode == "COMBINED")
            {
                if (model.OpenGymDurationDaysSeparate == null || model.OpenGymDurationDaysSeparate < 1)
                    ModelState.AddModelError(nameof(model.OpenGymDurationDaysSeparate),
                        "Open gym duration is required for combined packages.");
            }
        }
    }
}