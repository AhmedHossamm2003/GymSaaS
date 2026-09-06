using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize]
    public class CoachesController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IWebHostEnvironment _env;

        public CoachesController(GymDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET /Coaches
        [GymSaaS.Authorization.ViewPermissionAuthorize]
        public async Task<IActionResult> Index(string? search, Guid? branchId, bool? activeOnly)
        {
            var query = _db.Coaches
                .Where(c => c.TenantId == TenantId && !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c =>
                    c.FirstName.Contains(search) ||
                    c.LastName.Contains(search) ||
                    c.Specialty.Contains(search));

            var scopedBranchIds = User.AssignedBranchIds();
            if (scopedBranchIds.Count > 0)
                query = query.Where(c => scopedBranchIds.Contains(c.BranchId)
                    || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                        ub.UserId == c.UserId.Value && ub.IsActive
                        && scopedBranchIds.Contains(ub.BranchId))));

            if (branchId.HasValue && User.CanAccessBranch(branchId.Value))
                query = query.Where(c => c.BranchId == branchId.Value
                    || (c.UserId.HasValue && _db.UserBranches.Any(ub =>
                        ub.UserId == c.UserId.Value && ub.BranchId == branchId.Value && ub.IsActive)));

            if (activeOnly == true)
                query = query.Where(c => c.IsActive);

            var raw = await query
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(c => new
                {
                    c.CoachId,
                    c.FirstName,
                    c.LastName,
                    c.Specialty,
                    c.PhotoUrl,
                    c.Phone,
                    c.Email,
                    c.IsActive,
                    c.CoachTarget,
                    c.CreatedAtUtc,
                    c.BranchId,
                    c.UserId,
                    PrimaryBranchName = c.Branch.BranchName,
                })
                .ToListAsync();

            var linkedUserIds = raw.Where(c => c.UserId.HasValue).Select(c => c.UserId!.Value).ToList();
            var branchRows = await _db.UserBranches
                .Where(ub => linkedUserIds.Contains(ub.UserId) && ub.IsActive
                          && ub.Branch.TenantId == TenantId && ub.Branch.IsActive)
                .OrderBy(ub => ub.Branch.BranchName)
                .Select(ub => new { ub.UserId, ub.BranchId, ub.Branch.BranchName })
                .ToListAsync();
            var branchesByUser = branchRows
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.BranchName).Distinct().ToList());

            var coachIds = raw.Select(x => x.CoachId).ToList();
            var classCounts = await _db.GymClasses
                .Where(g => coachIds.Contains(g.CoachId!.Value) && !g.IsDeleted)
                .GroupBy(g => g.CoachId)
                .Select(g => new { CoachId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CoachId!.Value, x => x.Count);

            var traineeCounts = await _db.MemberPackages
                .Where(mp => coachIds.Contains(mp.CoachId!.Value) && mp.Status == "ACTIVE")
                .GroupBy(mp => mp.CoachId)
                .Select(g => new { CoachId = g.Key, Count = g.Select(mp => mp.MemberId).Distinct().Count() })
                .ToDictionaryAsync(x => x.CoachId!.Value, x => x.Count);

            var coaches = raw.Select(x => new CoachListItem
            {
                CoachId = x.CoachId,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Specialty = x.Specialty,
                PhotoUrl = x.PhotoUrl,
                Phone = x.Phone,
                Email = x.Email,
                IsActive = x.IsActive,
                BranchName = x.UserId.HasValue && branchesByUser.TryGetValue(x.UserId.Value, out var names)
                    ? string.Join(", ", names)
                    : x.PrimaryBranchName,
                BranchId = x.BranchId,
                CreatedAtUtc = x.CreatedAtUtc,
                ClassCount = classCounts.TryGetValue(x.CoachId, out var cnt) ? cnt : 0,
                ActiveTraineeCount = traineeCounts.TryGetValue(x.CoachId, out var tcnt) ? tcnt : 0,
                CoachTarget = x.CoachTarget,
            }).ToList();

            ViewData["Title"] = "Coaches";
            ViewData["Search"] = search;
            ViewData["BranchId"] = branchId;
            ViewData["ActiveOnly"] = activeOnly;
            ViewData["Branches"] = await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive
                         && (scopedBranchIds.Count == 0 || scopedBranchIds.Contains(b.BranchId)))
                .OrderBy(b => b.BranchName)
                .ToListAsync();
            ViewData["TotalCount"] = coaches.Count;
            ViewData["ActiveCount"] = coaches.Count(c => c.IsActive);

            return View(coaches);
        }

        // GET /Coaches/Create
        [Authorize(Policy = "ManagerAndAbove")]
        public async Task<IActionResult> Create()
        {
            var vm = new CoachFormViewModel
            {
                Branches = await GetBranchesAsync(),
            };
            ViewData["Title"] = "Coaches";
            ViewData["Subtitle"] = "New Coach";
            return View("CreateEdit", vm);
        }

        // POST /Coaches/Create
        [HttpPost]
        [Authorize(Policy = "ManagerAndAbove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoachFormViewModel model)
        {
            model.BranchIds = model.BranchIds.Where(id => id != Guid.Empty).Distinct().ToList();
            var availableBranches = await GetBranchesAsync();
            var availableBranchIds = availableBranches.Select(b => b.BranchId).ToHashSet();
            if (model.BranchIds.Count == 0)
                ModelState.AddModelError(nameof(model.BranchIds), "Select at least one branch.");
            else if (model.BranchIds.Any(id => !availableBranchIds.Contains(id)))
                ModelState.AddModelError(nameof(model.BranchIds), "One or more selected branches are not available.");
            else
                model.BranchId = model.BranchIds[0];

            // Required for auto-creating the User account
            if (string.IsNullOrWhiteSpace(model.Email))
                ModelState.AddModelError(nameof(model.Email), "Email is required (used for the coach's login account).");

            if (string.IsNullOrWhiteSpace(model.LoginPassword))
                ModelState.AddModelError(nameof(model.LoginPassword), "Login password is required.");

            // Ensure "Coach" role exists
            var coachRole = await _db.Roles
                .FirstOrDefaultAsync(r => r.TenantId == TenantId
                                       && r.RoleName == "Coach"
                                       && !r.IsDeleted
                                       && r.IsActive);

            if (coachRole == null)
                ModelState.AddModelError("", "The 'Coach' role does not exist. Please create it in the Roles page first.");

            // Email uniqueness check
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var normalizedEmail = model.Email.Trim().ToUpperInvariant();
                var emailTaken = await _db.Users.AnyAsync(u =>
                    u.TenantId == TenantId &&
                    u.DeletedAtUtc == null &&
                    u.NormalizedEmail == normalizedEmail);
                if (emailTaken)
                    ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            }

            if (!ModelState.IsValid)
            {
                model.Branches = availableBranches;
                ViewData["Title"] = "Coaches";
                ViewData["Subtitle"] = "New Coach";
                return View("CreateEdit", model);
            }

            var coachId = Guid.NewGuid();
            var photoUrl = await SavePhotoAsync(model.Photo, coachId);

            // 1. Create the User account
            // NOTE: AuthController compares passwords as plain text against PasswordHash.
            // Until that's migrated to real hashing, we store the password as-is to match.
            var userId = Guid.NewGuid();
            var emailTrim = model.Email!.Trim();

            var user = new User
            {
                UserId = userId,
                TenantId = TenantId,
                Email = emailTrim,
                NormalizedEmail = emailTrim.ToUpperInvariant(),
                PasswordHash = model.LoginPassword!,
                PasswordSalt = null,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
                IsActive = model.IsActive,
                IsLocked = false,
                CreatedAtUtc = DateTime.UtcNow,
            };

            _db.Users.Add(user);

            // 2. Assign Coach role
            _db.UserRoles.Add(new UserRole
            {
                UserRoleId = Guid.NewGuid(),
                UserId = userId,
                RoleId = coachRole!.RoleId,
                AssignedAtUtc = DateTime.UtcNow,
            });

            // 3. Assign the coach account to every selected branch.
            foreach (var selectedBranchId in model.BranchIds)
            {
                _db.UserBranches.Add(new UserBranch
                {
                    UserBranchId = Guid.NewGuid(),
                    UserId = userId,
                    BranchId = selectedBranchId,
                    IsActive = true,
                    AssignedAtUtc = DateTime.UtcNow,
                });
            }

            // 4. Create the Coach record linked to the User
            var coach = new Coach
            {
                CoachId = coachId,
                TenantId = TenantId,
                BranchId = model.BranchId,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Specialty = model.Specialty.Trim(),
                Bio = model.Bio?.Trim(),
                Phone = model.Phone?.Trim(),
                Email = emailTrim.ToLowerInvariant(),
                PhotoUrl = photoUrl,
                CoachTarget = model.CoachTarget,
                UserId = userId,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.Coaches.Add(coach);
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Coach {coach.FirstName} {coach.LastName} created with login account ({emailTrim}).";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }


        // GET /Coaches/Edit/id
        [Authorize(Policy = "ManagerAndAbove")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var c = await _db.Coaches
                .FirstOrDefaultAsync(x => x.CoachId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (c == null) return NotFound();

            string? linkedEmail = null;
            if (c.UserId.HasValue)
            {
                linkedEmail = await _db.Users
                    .Where(u => u.UserId == c.UserId.Value)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();
            }

            var assignedBranchIds = c.UserId.HasValue
                ? await _db.UserBranches
                    .Where(ub => ub.UserId == c.UserId.Value && ub.IsActive)
                    .Select(ub => ub.BranchId)
                    .ToListAsync()
                : new List<Guid>();
            if (assignedBranchIds.Count == 0) assignedBranchIds.Add(c.BranchId);

            var vm = new CoachFormViewModel
            {
                CoachId = c.CoachId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Specialty = c.Specialty,
                Bio = c.Bio,
                Phone = c.Phone,
                Email = c.Email,
                BranchId = c.BranchId,
                BranchIds = assignedBranchIds,
                CoachTarget = c.CoachTarget,
                ExistingPhotoUrl = c.PhotoUrl,
                IsActive = c.IsActive,
                HasLinkedUser = c.UserId.HasValue,
                LinkedUserEmail = linkedEmail,
                Branches = await GetBranchesAsync(),
            };

            ViewData["Title"] = "Coaches";
            ViewData["Subtitle"] = "Edit Coach";
            return View("CreateEdit", vm);
        }

        // POST /Coaches/Edit/id
        [HttpPost]
        [Authorize(Policy = "ManagerAndAbove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CoachFormViewModel model)
        {
            var c = await _db.Coaches
                .FirstOrDefaultAsync(x => x.CoachId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (c == null) return NotFound();

            model.BranchIds = model.BranchIds.Where(branchId => branchId != Guid.Empty).Distinct().ToList();
            var availableBranches = await GetBranchesAsync();
            var availableBranchIds = availableBranches.Select(b => b.BranchId).ToHashSet();
            if (model.BranchIds.Count == 0)
                ModelState.AddModelError(nameof(model.BranchIds), "Select at least one branch.");
            else if (model.BranchIds.Any(branchId => !availableBranchIds.Contains(branchId)))
                ModelState.AddModelError(nameof(model.BranchIds), "One or more selected branches are not available.");
            else
                model.BranchId = model.BranchIds[0];

            if (!ModelState.IsValid)
            {
                model.ExistingPhotoUrl = c.PhotoUrl;
                model.Branches = availableBranches;
                model.HasLinkedUser = c.UserId.HasValue;
                model.LinkedUserEmail = c.UserId.HasValue
                    ? await _db.Users.Where(u => u.UserId == c.UserId.Value).Select(u => u.Email).FirstOrDefaultAsync()
                    : null;
                ViewData["Title"] = "Coaches";
                ViewData["Subtitle"] = "Edit Coach";
                return View("CreateEdit", model);
            }

            if (model.Photo != null)
            {
                DeletePhoto(c.PhotoUrl);
                c.PhotoUrl = await SavePhotoAsync(model.Photo, id);
            }

            c.FirstName = model.FirstName.Trim();
            c.LastName = model.LastName.Trim();
            c.Specialty = model.Specialty.Trim();
            c.Bio = model.Bio?.Trim();
            c.Phone = model.Phone?.Trim();
            c.Email = model.Email?.Trim().ToLowerInvariant();
            c.BranchId = model.BranchId;
            c.CoachTarget = model.CoachTarget;
            c.IsActive = model.IsActive;
            c.UpdatedAtUtc = DateTime.UtcNow;
            c.UpdatedByUserId = UserId;

            if (c.UserId.HasValue)
            {
                var linkedUser = await _db.Users.FirstOrDefaultAsync(u =>
                    u.UserId == c.UserId.Value && u.TenantId == TenantId && u.DeletedAtUtc == null);
                if (linkedUser != null)
                {
                    linkedUser.FirstName = c.FirstName;
                    linkedUser.LastName = c.LastName;
                    linkedUser.PhoneNumber = c.Phone;
                    linkedUser.IsActive = c.IsActive;
                    linkedUser.UpdatedAtUtc = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(model.LoginPassword))
                    {
                        // Keep the current authentication storage format so existing login remains compatible.
                        linkedUser.PasswordHash = model.LoginPassword;
                        linkedUser.PasswordSalt = null;
                    }

                    var existingAssignments = await _db.UserBranches
                        .Where(ub => ub.UserId == linkedUser.UserId)
                        .ToListAsync();
                    foreach (var assignment in existingAssignments)
                        assignment.IsActive = model.BranchIds.Contains(assignment.BranchId);

                    var existingBranchIds = existingAssignments.Select(ub => ub.BranchId).ToHashSet();
                    foreach (var selectedBranchId in model.BranchIds.Where(branchId => !existingBranchIds.Contains(branchId)))
                    {
                        _db.UserBranches.Add(new UserBranch
                        {
                            UserBranchId = Guid.NewGuid(),
                            UserId = linkedUser.UserId,
                            BranchId = selectedBranchId,
                            IsActive = true,
                            AssignedAtUtc = DateTime.UtcNow,
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"{c.FirstName} {c.LastName} updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // POST /Coaches/Delete/id (soft delete)
        [HttpPost]
        [Authorize(Policy = "ManagerAndAbove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var c = await _db.Coaches
                .FirstOrDefaultAsync(x => x.CoachId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (c == null) return NotFound();

            c.IsDeleted = true;
            c.IsActive = false;
            c.UpdatedAtUtc = DateTime.UtcNow;
            c.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Coach {c.FirstName} {c.LastName} removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // GET /Coaches/Dashboard
        [Authorize(Policy = "CoachAndAbove")]
        public async Task<IActionResult> Dashboard()
        {
            if (!User.IsInRole("Coach"))
                return Forbid();

            var coach = await _db.Coaches
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.TenantId == TenantId && !c.IsDeleted);

            if (coach == null)
            {
                TempData["Toast"] = "Your account is not linked to a coach profile.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("Index", "Coaches");
            }

            var coachId = coach.CoachId;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var monthStartUtc = monthStart.ToDateTime(TimeOnly.MinValue);
            var sevenDays = today.AddDays(7);
            var thirtyDays = today.AddDays(30);
            var todayDow = (int)DateTime.Today.DayOfWeek;

            // Client ownership comes only from the coach assigned to the PT plan.
            // Delivering a guest session earns commission but does not transfer ownership.
            var activePtPackages = await _db.MemberPackages
                .Where(mp => mp.Status == "ACTIVE"
                          && mp.TenantId == TenantId
                          && mp.PackageType.PackageTypeCode == "PERSONAL_TRAINING"
                          && mp.CoachId == coachId)
                .Select(mp => new
                {
                    mp.MemberId,
                    mp.MemberPackageId,
                    mp.PackageNameSnapshot,
                    mp.SessionCountRemaining,
                    mp.ValidToDate,
                    mp.ValidFromDate,
                    mp.DurationDays,
                    mp.CreatedAtUtc,
                    mp.PriceSnapshot,
                    mp.CoachCommissionPercent,
                })
                .ToListAsync();

            // Compute trainee counts (one row per distinct member)
            var distinctTrainees = activePtPackages
                .GroupBy(p => p.MemberId)
                .Select(g => g.First())
                .ToList();

            var activeTraineesCount = distinctTrainees.Count;
            var newTraineesThisMonth = distinctTrainees.Count(p => p.CreatedAtUtc >= monthStartUtc);
            var sessionsRemainingTotal = activePtPackages.Sum(
                p => p.SessionCountRemaining ?? 0);

            // Expiring soon (next 30 days)
            var expiringSoon = activePtPackages
                .Where(p => p.ValidToDate.HasValue && p.ValidToDate.Value <= thirtyDays && p.ValidToDate.Value >= today)
                .ToList();

            // Sessions delivered this month are credited to the coach selected
            // at reception, independent of the assigned coach on the package.
            var sessionsDeliveredThisMonth = await _db.MemberPerkUsages
                .Where(u => u.TenantId == TenantId
                         && u.CoachId == coachId
                         && u.PerkType == "PT"
                         && u.UsedAtUtc >= monthStartUtc)
                .CountAsync();

            // Upcoming expirations (top 5 nearest)
            var memberNames = await _db.Members
                .Where(m => expiringSoon.Select(e => e.MemberId).Distinct().Contains(m.MemberId))
                .ToDictionaryAsync(m => m.MemberId, m => (m.FirstName + " " + m.LastName).Trim());

            var upcoming = expiringSoon
                .OrderBy(p => p.ValidToDate)
                .Take(5)
                .Select(p => new CoachUpcomingTraineeItem
                {
                    MemberId = p.MemberId,
                    MemberName = memberNames.TryGetValue(p.MemberId, out var n) ? n : "—",
                    PackageName = p.PackageNameSnapshot,
                    ValidToDate = p.ValidToDate ?? today,
                })
                .ToList();

            // Classes
            var allClasses = await _db.GymClasses
                .Where(g => g.CoachId == coachId && g.IsActive && !g.IsDeleted)
                .Select(g => new CoachClassScheduleItem
                {
                    GymClassId = g.GymClassId,
                    ClassName = g.ClassName,
                    DayOfWeek = g.DayOfWeek,
                    StartTime = g.StartTime,
                    EndTime = g.EndTime,
                    Capacity = g.Capacity,
                })
                .ToListAsync();

            var todayClasses = allClasses
                .Where(c => c.DayOfWeek == todayDow)
                .OrderBy(c => c.StartTime)
                .ToList();

            // Coach earnings are created per delivered PT session.
            var allEarningsRows = await _db.MemberPerkUsages
                .Where(u => u.CoachId == coachId
                         && u.TenantId == TenantId
                         && u.PerkType == "PT"
                         && u.CommissionAmount != null
                         && u.MemberPackageId != null)
                .Join(_db.MemberPackages,
                      u => u.MemberPackageId!.Value,
                      mp => mp.MemberPackageId,
                      (u, mp) => new
                {
                    mp.MemberPackageId,
                    u.MemberId,
                    mp.PackageNameSnapshot,
                    mp.PriceSnapshot,
                    CoachCommissionPercent = u.CommissionPercentSnapshot,
                    CommissionAmount = u.CommissionAmount,
                    CreatedAtUtc = u.UsedAtUtc,
                })
                .ToListAsync();

            var earningsThisMonth = allEarningsRows
                .Where(r => r.CreatedAtUtc >= monthStartUtc)
                .Sum(r => r.CommissionAmount ?? 0m);

            var earningsAllTime = allEarningsRows.Sum(r => r.CommissionAmount ?? 0m);

            var commissionSessionsThisMonth = allEarningsRows.Count(r => r.CreatedAtUtc >= monthStartUtc);

            // Recent earnings (5 latest) — resolve member names
            var recentEarningsRaw = allEarningsRows
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(5)
                .ToList();

            var recentMemberIds = recentEarningsRaw.Select(r => r.MemberId).Distinct().ToList();
            var recentMemberNames = await _db.Members
                .Where(m => recentMemberIds.Contains(m.MemberId))
                .ToDictionaryAsync(m => m.MemberId, m => (m.FirstName + " " + m.LastName).Trim());

            var recentEarnings = recentEarningsRaw
                .Select(r => new CoachEarningsItem
                {
                    MemberPackageId = r.MemberPackageId,
                    MemberId = r.MemberId,
                    MemberName = recentMemberNames.TryGetValue(r.MemberId, out var n) ? n : "—",
                    PackageName = r.PackageNameSnapshot,
                    PackagePrice = r.PriceSnapshot ?? 0m,
                    CommissionPercent = r.CoachCommissionPercent ?? 0m,
                    CommissionAmount = r.CommissionAmount ?? 0m,
                    EarnedAtUtc = r.CreatedAtUtc,
                })
                .ToList();

            var vm = new CoachDashboardViewModel
            {
                CoachId = coachId,
                FullName = $"{coach.FirstName} {coach.LastName}".Trim(),
                Specialty = coach.Specialty,
                PhotoUrl = coach.PhotoUrl,
                ActiveTraineeCount = activeTraineesCount,
                CoachTarget = coach.CoachTarget,
                ClassCount = allClasses.Count,
                NewTraineesThisMonth = newTraineesThisMonth,
                ExpiringSoonCount = expiringSoon.Count,
                SessionsRemainingTotal = sessionsRemainingTotal,
                SessionsDeliveredThisMonth = sessionsDeliveredThisMonth,
                TodayClassesCount = todayClasses.Count,
                TodayClasses = todayClasses,
                AllClasses = allClasses,
                UpcomingExpirations = upcoming,
                EarningsThisMonth = earningsThisMonth,
                EarningsAllTime = earningsAllTime,
                CommissionSessionsThisMonth = commissionSessionsThisMonth,
                RecentEarnings = recentEarnings,
            };

            ViewData["Title"] = "My Dashboard";
            return View(vm);
        }

        // GET /Coaches/MyTrainees
        [Authorize(Policy = "CoachAndAbove")]
        public async Task<IActionResult> MyTrainees()
        {
            if (!User.IsInRole("Coach"))
                return Forbid();

            var coach = await _db.Coaches
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.TenantId == TenantId && !c.IsDeleted);

            if (coach == null)
            {
                TempData["Toast"] = "Your account is not linked to a coach profile.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("Index", "Coaches");
            }

            var coachId = coach.CoachId;

            var trainees = await _db.MemberPackages
                .Where(mp => mp.Status == "ACTIVE"
                          && mp.TenantId == TenantId
                          && mp.PackageType.PackageTypeCode == "PERSONAL_TRAINING"
                          && mp.CoachId == coachId)
                .Join(_db.Members, mp => mp.MemberId, m => m.MemberId, (mp, m) => new { mp, m })
                .Select(x => new CoachMyTraineeItem
                {
                    MemberPackageId = x.mp.MemberPackageId,
                    MemberId = x.m.MemberId,
                    MemberName = (x.m.FirstName + " " + x.m.LastName).Trim(),
                    PackageName = x.mp.PackageNameSnapshot,
                    TrainingType = "Personal Training",
                    SessionsRemaining = x.mp.SessionCountRemaining,
                    DaysRemaining = x.mp.DurationDays,
                    ValidToDate = x.mp.ValidToDate ?? x.mp.ValidFromDate.AddDays(x.mp.DurationDays ?? 30),
                    Status = x.mp.Status,
                })
                .OrderBy(t => t.ValidToDate)
                .ToListAsync();

            ViewData["Title"] = "My Trainees";
            return View(trainees);
        }

        // GET /Coaches/MySchedule
        [Authorize(Policy = "CoachAndAbove")]
        public async Task<IActionResult> MySchedule()
        {
            if (!User.IsInRole("Coach"))
                return Forbid();

            var coach = await _db.Coaches
                .FirstOrDefaultAsync(c => c.UserId == UserId && c.TenantId == TenantId && !c.IsDeleted);

            if (coach == null)
            {
                TempData["Toast"] = "Your account is not linked to a coach profile.";
                TempData["ToastType"] = "warning";
                return RedirectToAction("Index", "Coaches");
            }

            var coachId = coach.CoachId;

            var classes = await _db.GymClasses
                .Where(g => g.CoachId == coachId && g.IsActive && !g.IsDeleted)
                .OrderBy(g => g.DayOfWeek)
                .ThenBy(g => g.StartTime)
                .Select(g => new CoachClassScheduleItem
                {
                    GymClassId = g.GymClassId,
                    ClassName = g.ClassName,
                    DayOfWeek = g.DayOfWeek,
                    StartTime = g.StartTime,
                    EndTime = g.EndTime,
                    Capacity = g.Capacity,
                })
                .ToListAsync();

            var vm = new CoachMyScheduleViewModel
            {
                CoachId = coachId,
                Classes = classes,
            };

            ViewData["Title"] = "My Schedule";
            return View(vm);
        }

        // ── Helpers ──────────────────────────────────

        private async Task<List<BranchDropdownItem>> GetBranchesAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem { BranchId = b.BranchId, BranchName = b.BranchName })
                .ToListAsync();

        private async Task<string?> SavePhotoAsync(IFormFile? file, Guid coachId)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "coaches");
            Directory.CreateDirectory(folder);

            var fileName = $"{coachId}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/coaches/{fileName}";
        }

        private void DeletePhoto(string? photoUrl)
        {
            if (string.IsNullOrEmpty(photoUrl)) return;
            var filePath = Path.Combine(_env.WebRootPath, photoUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}
