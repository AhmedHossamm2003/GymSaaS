using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "AnyStaff")]
    public class MemberPackagesController : Controller
    {
        private readonly GymDbContext _db;

        public MemberPackagesController(GymDbContext db) => _db = db;

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /MemberPackages/Assign?memberId=xxx
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Assign(Guid memberId)
        {
            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == memberId
                                       && m.TenantId == TenantId
                                       && !m.IsDeleted);

            if (member == null) return NotFound();

            // Resolve home branch name
            var homeBranch = await _db.Branches
                .Where(b => b.BranchId == member.HomeBranchId)
                .Select(b => new { b.BranchId, b.BranchName })
                .FirstOrDefaultAsync();

            var vm = new AssignPackageViewModel
            {
                MemberId = memberId,
                MemberName = $"{member.FirstName} {member.LastName}".Trim(),
                MembershipNumber = member.MembershipNumber,
                HomeBranchId = member.HomeBranchId,
                HomeBranchName = homeBranch?.BranchName ?? "—",
                CustomStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                AvailablePackages = await GetAvailablePackagesAsync(),
                CurrentPackages = await GetCurrentPackagesAsync(memberId),
                AllBranches = await GetBranchesAsync(),
            };

            ViewData["Title"] = vm.MemberName;
            ViewData["Subtitle"] = "Assign Package";
            return View(vm);
        }

        // ─────────────────────────────────────────────
        // POST /MemberPackages/Assign
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AssignPackageViewModel model, List<Guid> selectedBranchIds)
        {
            if (model.PackageDefinitionId == null)
                ModelState.AddModelError(nameof(model.PackageDefinitionId), "Please select a package.");

            if (!ModelState.IsValid)
            {
                model.AvailablePackages = await GetAvailablePackagesAsync();
                model.CurrentPackages = await GetCurrentPackagesAsync(model.MemberId);
                model.AllBranches = await GetBranchesAsync();
                ViewData["Title"] = model.MemberName;
                ViewData["Subtitle"] = "Assign Package";
                return View(model);
            }

            var pkgDef = await _db.PackageDefinitions
                .Include(p => p.PackageType)
                .Include(p => p.BranchAccessPolicyType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == model.PackageDefinitionId
                                       && p.TenantId == TenantId);

            if (pkgDef == null) return NotFound();

            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == model.MemberId && m.TenantId == TenantId);

            if (member == null) return NotFound();

            var startDate = model.CustomStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var typeCode = pkgDef.PackageType?.PackageTypeCode ?? "";

            // ── Build final allowed branch list (home always included) ─────────
            List<Guid> finalBranchIds = new();
            if (pkgDef.BranchAccessPolicyType?.PolicyCode == "SELECTED_BRANCHES")
            {
                // Always add home branch first
                finalBranchIds.Add(member.HomeBranchId);
                // Then add any extra branches the admin selected (excluding duplicates)
                foreach (var bid in selectedBranchIds)
                    if (!finalBranchIds.Contains(bid))
                        finalBranchIds.Add(bid);
            }

            // ── COMBINED — create two linked rows ─────────────────────────────
            if (typeCode == "COMBINED")
            {
                var groupId = Guid.NewGuid();

                // Row 1: Sessions component
                var sessionPkgId = Guid.NewGuid();
                var sessionExpiry = model.CustomSessionExpiry ?? startDate.AddDays(pkgDef.DurationDays ?? 30);
                var sessionCount = model.CustomSessionCount ?? pkgDef.SessionCount ?? 0;
                var sessionTypeId = await GetTypeIdAsync("SESSION");

                var sessionPkg = new MemberPackage
                {
                    MemberPackageId = sessionPkgId,
                    TenantId = TenantId,
                    MemberId = model.MemberId,
                    PackageDefinitionId = pkgDef.PackageDefinitionId,
                    PackageNameSnapshot = pkgDef.PackageName,
                    PackageTypeId = sessionTypeId,
                    BranchAccessPolicyTypeId = pkgDef.BranchAccessPolicyTypeId,
                    HomeBranchId = member.HomeBranchId,
                    Status = "ACTIVE",
                    IsCustomPackage = model.CustomSessionCount.HasValue,
                    SessionCountOriginal = sessionCount,
                    SessionCountRemaining = sessionCount + model.CarryOverSessions,
                    CarryOverSessionsAdded = model.CarryOverSessions,
                    DurationDays = pkgDef.DurationDays,
                    ValidFromDate = startDate,
                    ValidToDate = sessionExpiry,
                    Notes = model.Notes,
                    LinkedPackageGroupId = groupId,
                    PackageComponentRole = "SESSION",
                    OpenGymDailyLimit = 1,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = UserId,
                };

                // Row 2: Open gym component
                var gymPkgId = Guid.NewGuid();
                var gymExpiry = model.CustomOpenGymExpiry ?? startDate.AddDays(pkgDef.OpenGymDurationDays ?? pkgDef.DurationDays ?? 30);
                var gymTypeId = await GetTypeIdAsync("OPEN_GYM");

                var gymPkg = new MemberPackage
                {
                    MemberPackageId = gymPkgId,
                    TenantId = TenantId,
                    MemberId = model.MemberId,
                    PackageDefinitionId = pkgDef.PackageDefinitionId,
                    PackageNameSnapshot = pkgDef.PackageName,
                    PackageTypeId = gymTypeId,
                    BranchAccessPolicyTypeId = pkgDef.BranchAccessPolicyTypeId,
                    HomeBranchId = member.HomeBranchId,
                    Status = "ACTIVE",
                    IsCustomPackage = model.CustomOpenGymExpiry.HasValue,
                    SessionCountOriginal = null,
                    SessionCountRemaining = null,
                    CarryOverSessionsAdded = 0,
                    DurationDays = pkgDef.OpenGymDurationDays ?? pkgDef.DurationDays,
                    ValidFromDate = startDate,
                    ValidToDate = gymExpiry,
                    Notes = model.Notes,
                    LinkedPackageGroupId = groupId,
                    PackageComponentRole = "OPEN_GYM",
                    OpenGymDailyLimit = pkgDef.OpenGymDailyLimit,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = UserId,
                };

                _db.MemberPackages.Add(sessionPkg);
                _db.MemberPackages.Add(gymPkg);
                await _db.SaveChangesAsync();

                // Save allowed branches for both rows
                if (finalBranchIds.Any())
                {
                    await SaveAllowedBranches(sessionPkgId, finalBranchIds);
                    await SaveAllowedBranches(gymPkgId, finalBranchIds);
                }
            }
            else
            {
                // ── Single row (SESSION or OPEN_GYM) ──────────────────────────
                var pkgId = Guid.NewGuid();
                var typeId = pkgDef.PackageTypeId;
                var expiry = typeCode == "SESSION"
                    ? (model.CustomSessionExpiry ?? startDate.AddDays(pkgDef.DurationDays ?? 30))
                    : (model.CustomOpenGymExpiry ?? startDate.AddDays(pkgDef.DurationDays ?? 30));
                var sessions = typeCode == "SESSION"
                    ? (model.CustomSessionCount ?? pkgDef.SessionCount ?? 0)
                    : (int?)null;

                var pkg = new MemberPackage
                {
                    MemberPackageId = pkgId,
                    TenantId = TenantId,
                    MemberId = model.MemberId,
                    PackageDefinitionId = pkgDef.PackageDefinitionId,
                    PackageNameSnapshot = pkgDef.PackageName,
                    PackageTypeId = typeId,
                    BranchAccessPolicyTypeId = pkgDef.BranchAccessPolicyTypeId,
                    HomeBranchId = member.HomeBranchId,
                    Status = "ACTIVE",
                    IsCustomPackage = model.CustomSessionCount.HasValue || model.CustomSessionExpiry.HasValue,
                    SessionCountOriginal = sessions,
                    SessionCountRemaining = sessions.HasValue ? sessions + model.CarryOverSessions : null,
                    CarryOverSessionsAdded = model.CarryOverSessions,
                    DurationDays = pkgDef.DurationDays,
                    ValidFromDate = startDate,
                    ValidToDate = expiry,
                    Notes = model.Notes,
                    LinkedPackageGroupId = null,
                    PackageComponentRole = null,
                    OpenGymDailyLimit = pkgDef.OpenGymDailyLimit,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = UserId,
                };

                _db.MemberPackages.Add(pkg);
                await _db.SaveChangesAsync();

                if (finalBranchIds.Any())
                    await SaveAllowedBranches(pkgId, finalBranchIds);
            }

            TempData["Toast"] = $"Package \"{pkgDef.PackageName}\" assigned to {model.MemberName}.";
            TempData["ToastType"] = "success";
            return RedirectToAction("Details", "Members", new { id = model.MemberId });
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────
        private async Task<List<PackageDefinitionListItem>> GetAvailablePackagesAsync() =>
            await _db.PackageDefinitions
                .Where(p => p.TenantId == TenantId && p.IsActive)
                .Join(_db.PackageTypes, p => p.PackageTypeId, pt => pt.PackageTypeId, (p, pt) => new { p, pt })
                .Join(_db.BranchAccessPolicyTypes, x => x.p.BranchAccessPolicyTypeId, bp => bp.BranchAccessPolicyTypeId, (x, bp) => new { x.p, x.pt, bp })
                .OrderBy(x => x.pt.PackageTypeCode).ThenBy(x => x.p.SortOrder).ThenBy(x => x.p.PackageName)
                .Select(x => new PackageDefinitionListItem
                {
                    PackageDefinitionId = x.p.PackageDefinitionId,
                    PackageCode = x.p.PackageCode,
                    PackageName = x.p.PackageName,
                    PackageTypeCode = x.pt.PackageTypeCode,
                    PackageTypeName = x.pt.PackageTypeName,
                    BranchAccessPolicy = x.bp.PolicyCode,
                    SessionCount = x.p.SessionCount,
                    DurationDays = x.p.DurationDays,
                    OpenGymDurationDays = x.p.OpenGymDurationDays,
                    Price = x.p.Price,
                    IsActive = x.p.IsActive,
                    SortOrder = x.p.SortOrder,
                })
                .ToListAsync();

        private async Task<List<MemberPackageListItem>> GetCurrentPackagesAsync(Guid memberId) =>
            await _db.MemberPackages
                .Where(p => p.MemberId == memberId && p.Status == "ACTIVE")
                .Join(_db.PackageTypes, p => p.PackageTypeId, pt => pt.PackageTypeId, (p, pt) => new { p, pt })
                .Select(x => new MemberPackageListItem
                {
                    MemberPackageId = x.p.MemberPackageId,
                    PackageNameSnapshot = x.p.PackageNameSnapshot,
                    PackageTypeCode = x.pt.PackageTypeCode,
                    Status = x.p.Status,
                    ValidFromDate = x.p.ValidFromDate,
                    ValidToDate = x.p.ValidToDate,
                    SessionCountOriginal = x.p.SessionCountOriginal,
                    SessionCountRemaining = x.p.SessionCountRemaining,
                })
                .ToListAsync();

        private async Task<List<BranchDropdownItem>> GetBranchesAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem
                {
                    BranchId = b.BranchId,
                    BranchName = b.BranchName,
                })
                .ToListAsync();

        private async Task<Guid> GetTypeIdAsync(string typeCode) =>
            await _db.PackageTypes
                .Where(pt => pt.PackageTypeCode == typeCode)
                .Select(pt => pt.PackageTypeId)
                .FirstAsync();

        private async Task SaveAllowedBranches(Guid memberPackageId, List<Guid> branchIds)
        {
            foreach (var branchId in branchIds.Distinct())
            {
                _db.MemberPackageAllowedBranches.Add(new MemberPackageAllowedBranch
                {
                    AllowedBranchId = Guid.NewGuid(),
                    MemberPackageId = memberPackageId,
                    BranchId = branchId,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await _db.SaveChangesAsync();
        }
    }
}