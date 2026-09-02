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
                AvailablePackages = await GetAvailablePackagesAsync(member.HomeBranchId),
                CurrentPackages = await GetCurrentPackagesAsync(memberId),
                AllBranches = await GetBranchesAsync(),
                AvailableClasses = await GetClassesAsync(member.HomeBranchId),
                AvailableCoaches = await GetAvailableCoachesAsync(member.HomeBranchId),
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
            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == model.MemberId && m.TenantId == TenantId);

            if (member == null) return NotFound();

            if (model.PackageDefinitionId == null)
                ModelState.AddModelError(nameof(model.PackageDefinitionId), "Please select a package.");

            var pkgDef = await _db.PackageDefinitions
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == model.PackageDefinitionId
                                       && p.TenantId == TenantId);

            if (pkgDef?.IsPrivateTraining == true && model.CoachId == null)
                ModelState.AddModelError(nameof(model.CoachId), "A coach is required for private training packages.");

            // ── Enforce price floor (MaxDiscountedPrice) ─────────────────────
            if (pkgDef != null && model.FinalPrice.HasValue)
            {
                if (model.FinalPrice.Value < 0)
                    ModelState.AddModelError(nameof(model.FinalPrice),
                        "Price cannot be negative.");
                else if (pkgDef.Price.HasValue && model.FinalPrice.Value > pkgDef.Price.Value)
                    ModelState.AddModelError(nameof(model.FinalPrice),
                        $"Price cannot exceed the package price ({pkgDef.Price.Value:N2} EGP).");
                else if (pkgDef.MaxDiscountedPrice.HasValue
                      && model.FinalPrice.Value < pkgDef.MaxDiscountedPrice.Value)
                    ModelState.AddModelError(nameof(model.FinalPrice),
                        $"Discount limit reached — minimum price for this package is {pkgDef.MaxDiscountedPrice.Value:N2} EGP.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePackages = await GetAvailablePackagesAsync(member.HomeBranchId);
                model.CurrentPackages = await GetCurrentPackagesAsync(model.MemberId);
                model.AllBranches = await GetBranchesAsync();
                model.AvailableClasses = await GetClassesAsync(member.HomeBranchId);
                model.AvailableCoaches = await GetAvailableCoachesAsync(member.HomeBranchId);
                ViewData["Title"] = model.MemberName;
                ViewData["Subtitle"] = "Assign Package";
                return View(model);
            }

            pkgDef = await _db.PackageDefinitions
                .Include(p => p.PackageType)
                .Include(p => p.BranchAccessPolicyType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == pkgDef.PackageDefinitionId
                                       && p.TenantId == TenantId);

            if (pkgDef == null) return NotFound();

            var startDate = model.CustomStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var typeCode = pkgDef.PackageType?.PackageTypeCode ?? "";

            // ── Snapshot pricing + coach commission at moment of assignment ────
            // Use the staff-entered FinalPrice (after any discount) if provided;
            // otherwise fall back to the package's catalog price.
            decimal? priceSnap = model.FinalPrice ?? pkgDef.Price;
            decimal? commissionPct = null;
            decimal? commissionAmount = null;
            if (pkgDef.IsPrivateTraining && pkgDef.CoachCommissionPercent.HasValue && priceSnap.HasValue)
            {
                commissionPct = pkgDef.CoachCommissionPercent.Value;
                commissionAmount = Math.Round(priceSnap.Value * commissionPct.Value / 100m, 2);
            }

            // Resolve perks — use override if provided, else use catalog defaults
            int? invitationsTotal   = model.CustomInvitationCount   ?? pkgDef.InvitationCount;
            int? inBodyTotal        = model.CustomInBodyCount        ?? pkgDef.InBodyCount;
            int? ptSessionsTotal    = model.CustomPtSessionCount     ?? pkgDef.PtSessionCount;
            int? freezeAllowance    = model.CustomFreezeAllowanceDays ?? pkgDef.FreezeAllowanceDays;

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
                    CrossBranchVisitLimit = pkgDef.CrossBranchVisitLimit,
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
                    CoachId = model.CoachId,
                    // Perks — stored on the SESSION component row
                    InvitationsTotal     = invitationsTotal,
                    InvitationsRemaining = invitationsTotal,
                    InBodyTotal          = inBodyTotal,
                    InBodyRemaining      = inBodyTotal,
                    PtSessionsTotal      = ptSessionsTotal,
                    PtSessionsRemaining  = ptSessionsTotal,
                    FreezeAllowanceDays  = freezeAllowance,
                    FreezeRemainingDays  = freezeAllowance,
                    GymClassId           = model.GymClassId ?? pkgDef.GymClassId,
                    // Commission snapshot attaches to the SESSION component of COMBINED
                    PriceSnapshot          = priceSnap,
                    CoachCommissionPercent = commissionPct,
                    CoachCommissionAmount  = commissionAmount,
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
                    CrossBranchVisitLimit = pkgDef.CrossBranchVisitLimit,
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
                    CoachId = model.CoachId,
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
                    CrossBranchVisitLimit = pkgDef.CrossBranchVisitLimit,
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
                    CoachId = model.CoachId,
                    // Perks
                    InvitationsTotal     = invitationsTotal,
                    InvitationsRemaining = invitationsTotal,
                    InBodyTotal          = inBodyTotal,
                    InBodyRemaining      = inBodyTotal,
                    PtSessionsTotal      = ptSessionsTotal,
                    PtSessionsRemaining  = ptSessionsTotal,
                    FreezeAllowanceDays  = freezeAllowance,
                    FreezeRemainingDays  = freezeAllowance,
                    GymClassId           = (typeCode == "SESSION" || typeCode == "CLASS")
                        ? (model.GymClassId ?? pkgDef.GymClassId)
                        : null,
                    PriceSnapshot          = priceSnap,
                    CoachCommissionPercent = commissionPct,
                    CoachCommissionAmount  = commissionAmount,
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
        private async Task<List<PackageDefinitionListItem>> GetAvailablePackagesAsync(Guid homeBranchId)
        {
            var items = await _db.PackageDefinitions
                .Where(p => p.TenantId == TenantId && p.IsActive
                         && (p.RestrictedToBranchId == null || p.RestrictedToBranchId == homeBranchId))
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
                    GymClassId = x.p.GymClassId,
                    InvitationCount = x.p.InvitationCount,
                    InBodyCount = x.p.InBodyCount,
                    PtSessionCount = x.p.PtSessionCount,
                    FreezeAllowanceDays = x.p.FreezeAllowanceDays,
                    Price = x.p.Price,
                    MaxDiscountedPrice = x.p.MaxDiscountedPrice,
                    IsPrivateTraining = x.p.IsPrivateTraining,
                    IsActive = x.p.IsActive,
                    SortOrder = x.p.SortOrder,
                })
                .ToListAsync();

            // Resolve class name + coach for packages that have a linked class
            var classIds = items.Where(i => i.GymClassId.HasValue).Select(i => i.GymClassId!.Value).Distinct().ToList();
            if (classIds.Count > 0)
            {
                var classInfo = await _db.GymClasses
                    .Where(g => classIds.Contains(g.GymClassId))
                    .Select(g => new { g.GymClassId, g.ClassName, g.CoachId })
                    .ToListAsync();

                var coachIds = classInfo.Where(c => c.CoachId.HasValue).Select(c => c.CoachId!.Value).Distinct().ToList();
                var coachInfo = coachIds.Count > 0
                    ? await _db.Coaches
                        .Where(c => coachIds.Contains(c.CoachId))
                        .Select(c => new { c.CoachId, FullName = (c.FirstName + " " + c.LastName).Trim() })
                        .ToDictionaryAsync(c => c.CoachId, c => c.FullName)
                    : new Dictionary<Guid, string>();

                var classMap = classInfo.ToDictionary(c => c.GymClassId);

                foreach (var item in items.Where(i => i.GymClassId.HasValue))
                {
                    if (classMap.TryGetValue(item.GymClassId!.Value, out var cls))
                    {
                        item.GymClassName = cls.ClassName;
                        item.CoachId = cls.CoachId;
                        if (cls.CoachId.HasValue && coachInfo.TryGetValue(cls.CoachId.Value, out var cn))
                            item.CoachName = cn;
                    }
                }
            }

            return items;
        }

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

        private async Task<List<ClassDropdownItem>> GetClassesAsync(Guid branchId)
        {
            var raw = await _db.GymClasses
                .Where(g => g.TenantId == TenantId && g.BranchId == branchId && g.IsActive && !g.IsDeleted)
                .OrderBy(g => g.DayOfWeek)
                .ThenBy(g => g.StartTime)
                .ThenBy(g => g.ClassName)
                .Select(g => new
                {
                    g.GymClassId,
                    g.ClassName,
                    g.DayOfWeek,
                    g.StartTime,
                    g.EndTime,
                    g.CoachId,
                })
                .ToListAsync();

            var coachIds = raw.Where(c => c.CoachId.HasValue).Select(c => c.CoachId!.Value).Distinct().ToList();
            var coachMap = coachIds.Count > 0
                ? await _db.Coaches
                    .Where(c => coachIds.Contains(c.CoachId))
                    .Select(c => new { c.CoachId, Name = c.FirstName + " " + c.LastName })
                    .ToDictionaryAsync(c => c.CoachId, c => c.Name.Trim())
                : new Dictionary<Guid, string>();

            string DayName(int d) => d switch
            {
                0 => "Sun", 1 => "Mon", 2 => "Tue", 3 => "Wed",
                4 => "Thu", 5 => "Fri", 6 => "Sat", _ => "?"
            };

            return raw.Select(g => new ClassDropdownItem
            {
                GymClassId  = g.GymClassId,
                ClassName   = g.ClassName,
                TimeDisplay = $"{DayName(g.DayOfWeek)} {g.StartTime:HH:mm}–{g.EndTime:HH:mm}",
                CoachId     = g.CoachId,
                CoachName   = g.CoachId.HasValue && coachMap.TryGetValue(g.CoachId.Value, out var cn) ? cn : null,
            }).ToList();
        }

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

        private async Task<List<CoachDropdownItem>> GetAvailableCoachesAsync(Guid branchId) =>
            await _db.Coaches
                .Where(c => c.TenantId == TenantId && c.BranchId == branchId && c.IsActive && !c.IsDeleted)
                .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                .Select(c => new CoachDropdownItem
                {
                    CoachId = c.CoachId,
                    FullName = (c.FirstName + " " + c.LastName).Trim(),
                    Specialty = c.Specialty,
                    BranchId = c.BranchId,
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