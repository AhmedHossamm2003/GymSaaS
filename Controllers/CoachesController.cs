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
        [Authorize(Policy = "ManagerAndAbove")]
        public async Task<IActionResult> Index(string? search, Guid? branchId, bool? activeOnly)
        {
            var query = _db.Coaches
                .Where(c => c.TenantId == TenantId && !c.IsDeleted)
                .Join(_db.Branches,
                      c => c.BranchId,
                      b => b.BranchId,
                      (c, b) => new { c, b });

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x =>
                    x.c.FirstName.Contains(search) ||
                    x.c.LastName.Contains(search) ||
                    x.c.Specialty.Contains(search));

            var scopedBranchIds = User.AssignedBranchIds();
            if (scopedBranchIds.Count > 0)
                query = query.Where(x => scopedBranchIds.Contains(x.c.BranchId));

            if (branchId.HasValue && User.CanAccessBranch(branchId.Value))
                query = query.Where(x => x.c.BranchId == branchId.Value);

            if (activeOnly == true)
                query = query.Where(x => x.c.IsActive);

            var raw = await query
                .OrderByDescending(x => x.c.CreatedAtUtc)
                .Select(x => new
                {
                    x.c.CoachId,
                    x.c.FirstName,
                    x.c.LastName,
                    x.c.Specialty,
                    x.c.PhotoUrl,
                    x.c.Phone,
                    x.c.Email,
                    x.c.IsActive,
                    x.c.CoachTarget,
                    x.c.CreatedAtUtc,
                    x.c.BranchId,
                    BranchName = x.b.BranchName,
                })
                .ToListAsync();

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
                BranchName = x.BranchName,
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
                model.Branches = await GetBranchesAsync();
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

            // 3. Assign user to coach's branch
            _db.UserBranches.Add(new UserBranch
            {
                UserBranchId = Guid.NewGuid(),
                UserId = userId,
                BranchId = model.BranchId,
                IsActive = true,
                AssignedAtUtc = DateTime.UtcNow,
            });

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

            if (!ModelState.IsValid)
            {
                model.ExistingPhotoUrl = c.PhotoUrl;
                model.Branches = await GetBranchesAsync();
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

            // Pull active PT packages once
            var activePtPackages = await _db.MemberPackages
                .Where(mp => mp.CoachId == coachId && mp.Status == "ACTIVE")
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
                    mp.CoachCommissionAmount,
                })
                .ToListAsync();

            // Compute trainee counts (one row per distinct member)
            var distinctTrainees = activePtPackages
                .GroupBy(p => p.MemberId)
                .Select(g => g.First())
                .ToList();

            var activeTraineesCount = distinctTrainees.Count;
            var newTraineesThisMonth = distinctTrainees.Count(p => p.CreatedAtUtc >= monthStartUtc);
            var sessionsRemainingTotal = activePtPackages.Sum(p => p.SessionCountRemaining ?? 0);

            // Expiring soon (next 30 days)
            var expiringSoon = activePtPackages
                .Where(p => p.ValidToDate.HasValue && p.ValidToDate.Value <= thirtyDays && p.ValidToDate.Value >= today)
                .ToList();

            // Sessions delivered this month — attendance records linked to this coach's packages, this month
            var ptPackageIds = activePtPackages.Select(p => p.MemberPackageId).ToList();
            var sessionsDeliveredThisMonth = ptPackageIds.Count == 0 ? 0 : await _db.AttendanceRecords
                .Where(a => ptPackageIds.Contains(a.MemberPackageId ?? Guid.Empty)
                         && a.CheckInAtUtc >= monthStartUtc)
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

            // ── Coach earnings — all-time and this month (commission from private packages) ──
            var allEarningsRows = await _db.MemberPackages
                .Where(mp => mp.CoachId == coachId
                          && mp.TenantId == TenantId
                          && mp.CoachCommissionAmount != null
                          && mp.Status != "CANCELLED")
                .Select(mp => new
                {
                    mp.MemberPackageId,
                    mp.MemberId,
                    mp.PackageNameSnapshot,
                    mp.PriceSnapshot,
                    mp.CoachCommissionPercent,
                    mp.CoachCommissionAmount,
                    mp.CreatedAtUtc,
                })
                .ToListAsync();

            var earningsThisMonth = allEarningsRows
                .Where(r => r.CreatedAtUtc >= monthStartUtc)
                .Sum(r => r.CoachCommissionAmount ?? 0m);

            var earningsAllTime = allEarningsRows.Sum(r => r.CoachCommissionAmount ?? 0m);

            var commissionPackagesThisMonth = allEarningsRows.Count(r => r.CreatedAtUtc >= monthStartUtc);

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
                    CommissionAmount = r.CoachCommissionAmount ?? 0m,
                    AssignedAtUtc = r.CreatedAtUtc,
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
                CommissionPackagesThisMonth = commissionPackagesThisMonth,
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
                .Where(mp => mp.CoachId == coachId && mp.Status == "ACTIVE")
                .Join(_db.Members, mp => mp.MemberId, m => m.MemberId, (mp, m) => new { mp, m })
                .Select(x => new CoachMyTraineeItem
                {
                    MemberPackageId = x.mp.MemberPackageId,
                    MemberId = x.m.MemberId,
                    MemberName = (x.m.FirstName + " " + x.m.LastName).Trim(),
                    PackageName = x.mp.PackageNameSnapshot,
                    TrainingType = x.mp.SessionCountRemaining.HasValue ? "Classes" : "Open Gym",
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
