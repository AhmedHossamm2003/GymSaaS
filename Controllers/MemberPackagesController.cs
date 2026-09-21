using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [GymSaaS.Authorization.ViewPermissionAuthorize("Members")]
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
                .Include(p => p.PackageType)
                .Include(p => p.BranchAccessPolicyType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == model.PackageDefinitionId
                                       && p.TenantId == TenantId);

            if (pkgDef == null)
                ModelState.AddModelError(nameof(model.PackageDefinitionId),
                    "The selected package is no longer available.");

            var selectedTypeCode = pkgDef?.PackageType?.PackageTypeCode ?? "";
            if (model.CoachId.HasValue && selectedTypeCode == "PERSONAL_TRAINING")
            {
                var validAssignedCoach = await _db.Coaches.AnyAsync(c =>
                    c.CoachId == model.CoachId.Value
                    && c.TenantId == TenantId
                    && (c.BranchId == member.HomeBranchId
                        || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                            ub.UserId == c.UserId.Value
                            && ub.BranchId == member.HomeBranchId
                            && ub.IsActive)))
                    && c.IsActive
                    && !c.IsDeleted);

                if (!validAssignedCoach)
                    ModelState.AddModelError(nameof(model.CoachId),
                        "Please select an active coach assigned to the member's home branch.");
            }

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

            if (pkgDef == null) return NotFound();

            var startDate = model.CustomStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var typeCode = pkgDef.PackageType?.PackageTypeCode ?? "";
            var isPersonalTraining = typeCode == "PERSONAL_TRAINING";

            // Snapshot the charged price and commission rate. Commission money is
            // earned later, one delivered PT session at a time.
            // Use the staff-entered FinalPrice (after any discount) if provided;
            // otherwise fall back to the package's catalog price.
            decimal? priceSnap = model.FinalPrice ?? pkgDef.Price;
            // Coach cut can differ per member, so honour the staff-entered value when
            // provided and fall back to the package catalog rate otherwise.
            decimal? commissionPct = isPersonalTraining
                ? (model.CustomCoachCommissionPercent ?? pkgDef.CoachCommissionPercent)
                : null;

            // Resolve perks — use override if provided, else use catalog defaults
            int? invitationsTotal   = model.CustomInvitationCount   ?? pkgDef.InvitationCount;
            int? inBodyTotal        = model.CustomInBodyCount        ?? pkgDef.InBodyCount;
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

            // ── Single row (SESSION, OPEN_GYM, or PERSONAL_TRAINING) ──────────
                var pkgId = Guid.NewGuid();
                var typeId = pkgDef.PackageTypeId;
                var expiry = typeCode is "SESSION" or "PERSONAL_TRAINING"
                    ? (model.CustomSessionExpiry ?? startDate.AddDays(pkgDef.DurationDays ?? 30))
                    : (model.CustomOpenGymExpiry ?? startDate.AddDays(pkgDef.DurationDays ?? 30));
                var sessions = typeCode is "SESSION" or "PERSONAL_TRAINING"
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
                    // Optional client owner. The coach who actually delivers a session
                    // is still selected independently at reception.
                    CoachId = isPersonalTraining ? model.CoachId : null,
                    // Perks
                    InvitationsTotal     = invitationsTotal,
                    InvitationsRemaining = invitationsTotal,
                    InBodyTotal          = inBodyTotal,
                    InBodyRemaining      = inBodyTotal,
                    FreezeAllowanceDays  = freezeAllowance,
                    FreezeRemainingDays  = freezeAllowance,
                    GymClassId           = typeCode is "SESSION" or "CLASS"
                        ? (model.GymClassId ?? pkgDef.GymClassId)
                        : null,
                    PriceSnapshot          = priceSnap,
                    CoachCommissionPercent = commissionPct,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = UserId,
                };

                _db.MemberPackages.Add(pkg);
                await _db.SaveChangesAsync();

                if (finalBranchIds.Any())
                    await SaveAllowedBranches(pkgId, finalBranchIds);

            TempData["Toast"] = $"Package \"{pkgDef.PackageName}\" assigned to {model.MemberName}.";
            TempData["ToastType"] = "success";
            return RedirectToAction("Details", "Members", new { id = model.MemberId });
        }

        // ─────────────────────────────────────────────
        // GET /MemberPackages/ChangePlan?memberPackageId=xxx
        // ─────────────────────────────────────────────
        // Upgrade or downgrade a package the member already holds. What they paid
        // for the old plan is credited, and only the difference changes hands.
        public async Task<IActionResult> ChangePlan(Guid memberPackageId)
        {
            var current = await LoadChangeablePackageAsync(memberPackageId);
            if (current == null) return NotFound();

            var vm = await BuildChangePlanViewModelAsync(current);
            ViewData["Title"] = vm.MemberName;
            ViewData["Subtitle"] = "Change Plan";
            return View(vm);
        }

        // ─────────────────────────────────────────────
        // POST /MemberPackages/ChangePlan
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePlan(ChangePlanViewModel model, List<Guid> selectedBranchIds)
        {
            var current = await LoadChangeablePackageAsync(model.CurrentMemberPackageId);
            if (current == null) return NotFound();

            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == current.MemberId
                                       && m.TenantId == TenantId
                                       && !m.IsDeleted);
            if (member == null) return NotFound();

            var newDef = await _db.PackageDefinitions
                .Include(p => p.PackageType)
                .Include(p => p.BranchAccessPolicyType)
                .FirstOrDefaultAsync(p => p.PackageDefinitionId == model.NewPackageDefinitionId
                                       && p.TenantId == TenantId);

            if (newDef == null)
                ModelState.AddModelError(nameof(model.NewPackageDefinitionId),
                    "The selected plan is no longer available.");

            if (newDef != null && newDef.PackageDefinitionId == current.PackageDefinitionId)
                ModelState.AddModelError(nameof(model.NewPackageDefinitionId),
                    "That is the plan the member is already on — pick a different one.");

            var paidSoFar  = current.PriceSnapshot ?? current.CatalogPrice ?? 0m;
            var difference = model.AmountDifference ?? 0m;

            // The member's total outlay for the new plan is what they already paid
            // plus (or minus) whatever moves now. Hold that to the same ceiling and
            // discount floor the assign screen enforces.
            var effectiveNewPrice = paidSoFar + difference;

            if (newDef != null)
            {
                if (effectiveNewPrice < 0)
                    ModelState.AddModelError(nameof(model.AmountDifference),
                        "That refund is larger than what the member paid.");
                else if (newDef.Price.HasValue && effectiveNewPrice > newDef.Price.Value)
                    ModelState.AddModelError(nameof(model.AmountDifference),
                        $"Total would be {effectiveNewPrice:N2} EGP, above the plan price of {newDef.Price.Value:N2} EGP.");
                else if (newDef.MaxDiscountedPrice.HasValue
                      && effectiveNewPrice < newDef.MaxDiscountedPrice.Value)
                    ModelState.AddModelError(nameof(model.AmountDifference),
                        $"Discount limit reached — the member must end up paying at least {newDef.MaxDiscountedPrice.Value:N2} EGP for this plan.");
            }

            var newTypeCode = newDef?.PackageType?.PackageTypeCode ?? "";
            if (model.CoachId.HasValue && newTypeCode == "PERSONAL_TRAINING")
            {
                var validCoach = await _db.Coaches.AnyAsync(c =>
                    c.CoachId == model.CoachId.Value
                    && c.TenantId == TenantId
                    && (c.BranchId == member.HomeBranchId
                        || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                            ub.UserId == c.UserId.Value
                            && ub.BranchId == member.HomeBranchId
                            && ub.IsActive)))
                    && c.IsActive
                    && !c.IsDeleted);

                if (!validCoach)
                    ModelState.AddModelError(nameof(model.CoachId),
                        "Please select an active coach assigned to the member's home branch.");
            }

            if (!ModelState.IsValid)
            {
                var redo = await BuildChangePlanViewModelAsync(current);
                // Keep what the staffer typed rather than resetting the form.
                redo.NewPackageDefinitionId       = model.NewPackageDefinitionId;
                redo.AmountDifference             = model.AmountDifference;
                redo.PaymentMethod                = model.PaymentMethod;
                redo.CustomSessionCount           = model.CustomSessionCount;
                redo.CarryOverSessions            = model.CarryOverSessions;
                redo.CustomStartDate              = model.CustomStartDate;
                redo.CustomExpiryDate             = model.CustomExpiryDate;
                redo.CustomInvitationCount        = model.CustomInvitationCount;
                redo.CustomInBodyCount            = model.CustomInBodyCount;
                redo.CustomFreezeAllowanceDays    = model.CustomFreezeAllowanceDays;
                redo.GymClassId                   = model.GymClassId;
                redo.CoachId                      = model.CoachId;
                redo.CustomCoachCommissionPercent = model.CustomCoachCommissionPercent;
                redo.Reason                       = model.Reason;

                ViewData["Title"] = redo.MemberName;
                ViewData["Subtitle"] = "Change Plan";
                return View(redo);
            }

            var now       = DateTime.UtcNow;
            var startDate = model.CustomStartDate ?? DateOnly.FromDateTime(now);
            var isPersonalTraining = newTypeCode == "PERSONAL_TRAINING";

            var expiry = model.CustomExpiryDate
                      ?? startDate.AddDays(newDef!.DurationDays ?? 30);

            var sessions = newTypeCode is "SESSION" or "PERSONAL_TRAINING"
                ? (model.CustomSessionCount ?? newDef.SessionCount ?? 0)
                : (int?)null;

            int? invitationsTotal = model.CustomInvitationCount ?? newDef.InvitationCount;
            int? inBodyTotal      = model.CustomInBodyCount ?? newDef.InBodyCount;
            int? freezeAllowance  = model.CustomFreezeAllowanceDays ?? newDef.FreezeAllowanceDays;

            List<Guid> finalBranchIds = new();
            if (newDef.BranchAccessPolicyType?.PolicyCode == "SELECTED_BRANCHES")
            {
                finalBranchIds.Add(member.HomeBranchId);
                foreach (var bid in selectedBranchIds)
                    if (!finalBranchIds.Contains(bid))
                        finalBranchIds.Add(bid);
            }

            var oldPackage = await _db.MemberPackages
                .FirstAsync(mp => mp.MemberPackageId == current.MemberPackageId);

            using var tx = await _db.Database.BeginTransactionAsync();

            // Retire the old plan. REPLACED (not CANCELLED) so reporting still counts
            // the original sale in the period it happened — see ReportsService.
            oldPackage.Status            = "REPLACED";
            oldPackage.CancelledAtUtc    = now;
            oldPackage.CancelledByUserId = UserId;
            oldPackage.UpdatedAtUtc      = now;
            oldPackage.UpdatedByUserId   = UserId;

            var newId = Guid.NewGuid();
            var newPackage = new MemberPackage
            {
                MemberPackageId          = newId,
                TenantId                 = TenantId,
                MemberId                 = member.MemberId,
                PackageDefinitionId      = newDef.PackageDefinitionId,
                PackageNameSnapshot      = newDef.PackageName,
                PackageTypeId            = newDef.PackageTypeId,
                BranchAccessPolicyTypeId = newDef.BranchAccessPolicyTypeId,
                HomeBranchId             = member.HomeBranchId,
                CrossBranchVisitLimit    = newDef.CrossBranchVisitLimit,
                Status                   = "ACTIVE",
                IsCustomPackage          = model.CustomSessionCount.HasValue || model.CustomExpiryDate.HasValue,
                SessionCountOriginal     = sessions,
                SessionCountRemaining    = sessions.HasValue ? sessions + model.CarryOverSessions : null,
                CarryOverSessionsAdded   = model.CarryOverSessions,
                DurationDays             = newDef.DurationDays,
                ValidFromDate            = startDate,
                ValidToDate              = expiry,
                Notes                    = model.Reason,
                OpenGymDailyLimit        = newDef.OpenGymDailyLimit,
                CoachId                  = isPersonalTraining ? model.CoachId : null,
                InvitationsTotal         = invitationsTotal,
                InvitationsRemaining     = invitationsTotal,
                InBodyTotal              = inBodyTotal,
                InBodyRemaining          = inBodyTotal,
                FreezeAllowanceDays      = freezeAllowance,
                FreezeRemainingDays      = freezeAllowance,
                GymClassId               = newTypeCode is "SESSION" or "CLASS"
                    ? (model.GymClassId ?? newDef.GymClassId)
                    : null,
                // Full value of the new plan, not just the difference — PT commission
                // is derived from this snapshot.
                PriceSnapshot            = effectiveNewPrice,
                CoachCommissionPercent   = isPersonalTraining
                    ? (model.CustomCoachCommissionPercent ?? newDef.CoachCommissionPercent)
                    : null,
                PlanChangedFromMemberPackageId = oldPackage.MemberPackageId,
                PlanChangeAmount         = difference,
                CreatedAtUtc             = now,
                CreatedByUserId          = UserId,
            };

            _db.MemberPackages.Add(newPackage);
            await _db.SaveChangesAsync();

            if (finalBranchIds.Any())
                await SaveAllowedBranches(newId, finalBranchIds);

            // Move the money. An upgrade is income; a refund on a downgrade leaves the
            // till, so it is booked as an expense and Net Profit stays truthful.
            var today = DateOnly.FromDateTime(now);
            var moneyNote = $"Plan change: {oldPackage.PackageNameSnapshot} → {newDef.PackageName}"
                          + (string.IsNullOrWhiteSpace(model.Reason) ? "" : $" · {model.Reason}");

            if (difference > 0)
            {
                _db.ManualIncomeEntries.Add(new ManualIncomeEntry
                {
                    IncomeEntryId   = Guid.NewGuid(),
                    TenantId        = TenantId,
                    BranchId        = member.HomeBranchId,
                    CategoryCode    = "PLAN_CHANGE",
                    Description     = $"Plan upgrade — {member.FirstName} {member.LastName}".Trim(),
                    Amount          = difference,
                    IncomeDate      = today,
                    Notes           = moneyNote,
                    PaymentMethod   = model.PaymentMethod,
                    SourceCode      = "PLAN_CHANGE",
                    MemberId        = member.MemberId,
                    CreatedAtUtc    = now,
                    CreatedByUserId = UserId,
                });
            }
            else if (difference < 0)
            {
                _db.Expenses.Add(new Expense
                {
                    ExpenseId       = Guid.NewGuid(),
                    TenantId        = TenantId,
                    BranchId        = member.HomeBranchId,
                    CategoryCode    = "PLAN_REFUND",
                    // Expenses have no description field; the member goes in VendorName
                    // so the finance list shows who the refund went to.
                    VendorName      = $"{member.FirstName} {member.LastName}".Trim(),
                    Amount          = Math.Abs(difference),
                    ExpenseDate     = today,
                    Notes           = moneyNote,
                    PaymentMethod   = model.PaymentMethod,
                    CreatedAtUtc    = now,
                    CreatedByUserId = UserId,
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var verb = difference > 0 ? $"collected {difference:N2} EGP"
                     : difference < 0 ? $"refunded {Math.Abs(difference):N2} EGP"
                     : "no money moved";
            TempData["Toast"] = $"Plan changed to {newDef.PackageName} — {verb}.";
            TempData["ToastType"] = "success";
            return RedirectToAction("Details", "Members", new { id = member.MemberId });
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────

        // Shape of the package being replaced, with the price the member actually paid.
        private sealed class ChangeablePackage
        {
            public Guid MemberPackageId { get; init; }
            public Guid MemberId { get; init; }
            public Guid HomeBranchId { get; init; }
            public Guid? PackageDefinitionId { get; init; }
            public string PackageNameSnapshot { get; init; } = "";
            public string PackageTypeName { get; init; } = "";
            public int? SessionCountRemaining { get; init; }
            public DateOnly? ValidToDate { get; init; }
            public decimal? PriceSnapshot { get; init; }
            public decimal? CatalogPrice { get; init; }
            public string MemberName { get; init; } = "";
        }

        private async Task<ChangeablePackage?> LoadChangeablePackageAsync(Guid memberPackageId)
        {
            return await _db.MemberPackages
                .Where(mp => mp.MemberPackageId == memberPackageId
                          && mp.TenantId == TenantId
                          && mp.Status == "ACTIVE")
                .Select(mp => new ChangeablePackage
                {
                    MemberPackageId       = mp.MemberPackageId,
                    MemberId              = mp.MemberId,
                    HomeBranchId          = mp.HomeBranchId,
                    PackageDefinitionId   = mp.PackageDefinitionId,
                    PackageNameSnapshot   = mp.PackageNameSnapshot,
                    PackageTypeName       = mp.PackageType.PackageTypeName,
                    SessionCountRemaining = mp.SessionCountRemaining,
                    ValidToDate           = mp.ValidToDate,
                    PriceSnapshot         = mp.PriceSnapshot,
                    CatalogPrice          = mp.PackageDefinition != null ? mp.PackageDefinition.Price : null,
                    MemberName            = (mp.Member.FirstName + " " + mp.Member.LastName).Trim(),
                })
                .FirstOrDefaultAsync();
        }

        private async Task<ChangePlanViewModel> BuildChangePlanViewModelAsync(ChangeablePackage current)
        {
            return new ChangePlanViewModel
            {
                MemberId                 = current.MemberId,
                MemberName               = current.MemberName,
                CurrentMemberPackageId   = current.MemberPackageId,
                CurrentPackageDefinitionId = current.PackageDefinitionId,
                CurrentPackageName       = current.PackageNameSnapshot,
                CurrentPackageTypeName   = current.PackageTypeName,
                CurrentSessionsRemaining = current.SessionCountRemaining,
                CurrentValidTo           = current.ValidToDate,
                CurrentPaidAmount        = current.PriceSnapshot ?? current.CatalogPrice ?? 0m,
                CustomStartDate          = DateOnly.FromDateTime(DateTime.UtcNow),
                AvailablePackages        = await GetAvailablePackagesAsync(current.HomeBranchId),
                AvailableClasses         = await GetClassesAsync(current.HomeBranchId),
                AvailableCoaches         = await GetAvailableCoachesAsync(current.HomeBranchId),
            };
        }

        private async Task<List<PackageDefinitionListItem>> GetAvailablePackagesAsync(Guid homeBranchId)
        {
            var items = await _db.PackageDefinitions
                .Where(p => p.TenantId == TenantId && p.IsActive
                         && p.PackageType.PackageTypeCode != "COMBINED"
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
                    FreezeAllowanceDays = x.p.FreezeAllowanceDays,
                    Price = x.p.Price,
                    MaxDiscountedPrice = x.p.MaxDiscountedPrice,
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
                .Where(c => c.TenantId == TenantId && c.IsActive && !c.IsDeleted
                         && (c.BranchId == branchId
                             || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                                 ub.UserId == c.UserId.Value && ub.BranchId == branchId && ub.IsActive))))
                .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                .Select(c => new CoachDropdownItem
                {
                    CoachId = c.CoachId,
                    FullName = (c.FirstName + " " + c.LastName).Trim(),
                    Specialty = c.Specialty,
                    BranchId = c.BranchId,
                })
                .ToListAsync();

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
