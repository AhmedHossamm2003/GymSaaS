using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize]
    public class MembersController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IWebHostEnvironment _env;

        public MembersController(GymDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /Members
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index(string? search, string? status, Guid? branchId, int page = 1)
        {
            const int pageSize = 20;

            var query = _db.Members
                .Where(m => m.TenantId == TenantId && !m.IsDeleted)
                .Join(_db.MemberStatuses,
                      m => m.MemberStatusId,
                      ms => ms.MemberStatusId,
                      (m, ms) => new { m, ms })
                .Join(_db.Branches,
                      x => x.m.HomeBranchId,
                      b => b.BranchId,
                      (x, b) => new { x.m, x.ms, b });

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x =>
                    x.m.FirstName.Contains(search) ||
                    x.m.LastName.Contains(search) ||
                    x.m.MembershipNumber.Contains(search) ||
                    x.m.PhoneNumber.Contains(search) ||
                    x.m.Email.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(x => x.ms.StatusCode == status);

            if (branchId.HasValue)
                query = query.Where(x => x.m.HomeBranchId == branchId.Value);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var raw = await query
                .OrderByDescending(x => x.m.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.m.MemberId,
                    x.m.MembershipNumber,
                    x.m.FirstName,
                    x.m.LastName,
                    x.m.PhoneNumber,
                    x.m.Email,
                    x.m.ProfileImageUrl,
                    x.m.LastLoginAtUtc,
                    x.ms.StatusCode,
                    x.ms.StatusName,
                    HomeBranchName = x.b.BranchName,
                })
                .ToListAsync();

            var now = DateTime.UtcNow;

            var members = new List<MemberListItem>();

            foreach (var r in raw)
            {
                // Fetch ALL active packages for this member
                var activePkgs = await _db.MemberPackages
                    .Where(p => p.MemberId == r.MemberId && p.Status == "ACTIVE")
                    .Join(_db.PackageTypes,
                          p => p.PackageTypeId,
                          pt => pt.PackageTypeId,
                          (p, pt) => new { p, pt })
                    .OrderBy(x => x.p.ValidToDate)
                    .ToListAsync();

                var lastCheckIn = await _db.AttendanceRecords
                    .Where(a => a.MemberId == r.MemberId)
                    .OrderByDescending(a => a.CheckInAtUtc)
                    .Select(a => (DateTime?)a.CheckInAtUtc)
                    .FirstOrDefaultAsync();

                var packageSummaries = activePkgs.Select(x => new ActivePackageSummary
                {
                    MemberPackageId = x.p.MemberPackageId,
                    PackageName = x.p.PackageNameSnapshot,
                    PackageTypeCode = x.pt.PackageTypeCode,
                    // PackageComponentRole and LinkedPackageGroupId added after migration + re-scaffold
                    ComponentRole = null,
                    ValidFromDate = x.p.ValidFromDate,
                    ValidToDate = x.p.ValidToDate,
                    SessionsOriginal = x.p.SessionCountOriginal,
                    SessionsRemaining = x.p.SessionCountRemaining,
                    LinkedGroupId = null,
                }).ToList();

                members.Add(new MemberListItem
                {
                    MemberId = r.MemberId,
                    MembershipNumber = r.MembershipNumber,
                    FullName = $"{r.FirstName} {r.LastName}".Trim(),
                    PhoneNumber = r.PhoneNumber,
                    ProfileImageUrl = r.ProfileImageUrl,
                    StatusCode = r.StatusCode,
                    StatusName = r.StatusName,
                    HomeBranchName = r.HomeBranchName,
                    ActivePackages = packageSummaries,
                    LastCheckIn = lastCheckIn,
                });
            }

            ViewData["Title"] = "Members";
            ViewData["Search"] = search;
            ViewData["Status"] = status;
            ViewData["BranchId"] = branchId;
            ViewData["Statuses"] = await _db.MemberStatuses.OrderBy(s => s.StatusName).ToListAsync();
            ViewData["Branches"] = await _db.Branches.Where(b => b.TenantId == TenantId && b.IsActive).OrderBy(b => b.BranchName).ToListAsync();
            ViewData["TotalCount"] = totalCount;
            ViewData["Page"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;

            return View(members);
        }

        // ─────────────────────────────────────────────
        // GET /Members/Details/id
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Details(Guid id)
        {
            var m = await _db.Members
                .Include(x => x.MemberStatus)
                .Include(x => x.HomeBranch)
                .FirstOrDefaultAsync(x => x.MemberId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (m == null) return NotFound();

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var bom = new DateTime(now.Year, now.Month, 1);

            // Fetch all active packages
            var allPkgs = await _db.MemberPackages
                .Where(p => p.MemberId == id && p.Status == "ACTIVE")
                .Join(_db.PackageTypes, p => p.PackageTypeId, pt => pt.PackageTypeId, (p, pt) => new { p, pt })
                .OrderBy(x => x.p.ValidToDate)
                .ToListAsync();

            // Build grouped view: combined packages share a LinkedPackageGroupId
            var packageGroups = new List<MemberPackageGroup>();

            // Group by MemberPackageId for now — after migration + re-scaffold,
            // switch to LinkedPackageGroupId to group combined packages
            var grouped = allPkgs
                .GroupBy(x => x.p.MemberPackageId.ToString())
                .ToList();

            foreach (var g in grouped)
            {
                var components = g.Select(x => new MemberPackageListItem
                {
                    MemberPackageId = x.p.MemberPackageId,
                    // LinkedPackageGroupId + PackageComponentRole available after migration + re-scaffold
                    LinkedPackageGroupId = null,
                    PackageNameSnapshot = x.p.PackageNameSnapshot,
                    PackageTypeCode = x.pt.PackageTypeCode,
                    PackageComponentRole = null,
                    Status = x.p.Status,
                    IsCustomPackage = x.p.IsCustomPackage,
                    ValidFromDate = x.p.ValidFromDate,
                    ValidToDate = x.p.ValidToDate,
                    SessionCountOriginal = x.p.SessionCountOriginal,
                    SessionCountRemaining = x.p.SessionCountRemaining,
                    CrossBranchVisitsUsed = x.p.CrossBranchVisitsUsed,
                    CrossBranchVisitLimit = x.p.CrossBranchVisitLimit,
                    CreatedAtUtc = x.p.CreatedAtUtc,
                }).ToList();

                packageGroups.Add(new MemberPackageGroup
                {
                    LinkedGroupId = null,
                    GroupName = g.First().p.PackageNameSnapshot,
                    Components = components,
                });
            }

            var lastCheckIn = await _db.AttendanceRecords
                .Where(a => a.MemberId == id)
                .OrderByDescending(a => a.CheckInAtUtc)
                .Select(a => (DateTime?)a.CheckInAtUtc)
                .FirstOrDefaultAsync();

            var vm = new MemberDetailsViewModel
            {
                MemberId = m.MemberId,
                MembershipNumber = m.MembershipNumber,
                FirstName = m.FirstName,
                LastName = m.LastName,
                FullName = $"{m.FirstName} {m.LastName}".Trim(),
                Email = m.Email,
                PhoneNumber = m.PhoneNumber,
                ProfileImageUrl = m.ProfileImageUrl,
                Gender = m.Gender,
                DateOfBirth = m.DateOfBirth,
                HomeBranchName = m.HomeBranch?.BranchName ?? "—",
                StatusCode = m.MemberStatus?.StatusCode ?? "",
                StatusName = m.MemberStatus?.StatusName ?? "",
                EmergencyContactName = m.EmergencyContactName,
                EmergencyContactPhone = m.EmergencyContactPhone,
                Notes = m.Notes,
                CreatedAtUtc = m.CreatedAtUtc,
                LastLoginAtUtc = m.LastLoginAtUtc,
                LastCheckIn = lastCheckIn,
                PackageGroups = packageGroups,
                TotalAttendance = await _db.AttendanceRecords.CountAsync(a => a.MemberId == id),
                AttendanceThisMonth = await _db.AttendanceRecords.CountAsync(a => a.MemberId == id && a.CheckInAtUtc >= bom),
            };

            ViewData["Title"] = vm.FullName;
            ViewData["Subtitle"] = "Member Profile";
            return View(vm);
        }

        // ─────────────────────────────────────────────
        // GET /Members/Create
        // ─────────────────────────────────────────────
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Create()
        {
            var vm = new MemberFormViewModel
            {
                Branches = await GetBranchesAsync(),
            };
            ViewData["Title"] = "Members";
            ViewData["Subtitle"] = "New Member";
            return View("CreateEdit", vm);
        }

        // ─────────────────────────────────────────────
        // POST /Members/Create
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Create(MemberFormViewModel model)
        {
            await ValidateUniqueFields(model, null);

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"] = "Members";
                ViewData["Subtitle"] = "New Member";
                return View("CreateEdit", model);
            }

            var statusId = await _db.MemberStatuses
                .Where(s => s.StatusCode == "ACTIVE")
                .Select(s => s.MemberStatusId)
                .FirstOrDefaultAsync();

            var memberId = Guid.NewGuid();
            var memberNumber = await GenerateMembershipNumberAsync(model.FirstName, model.LastName);
            var imageUrl = await SaveProfileImageAsync(model.ProfileImage, memberId);
            var password = string.IsNullOrWhiteSpace(model.Password)
                                ? GeneratePassword(model.FirstName)
                                : model.Password;

            var member = new Member
            {
                MemberId = memberId,
                TenantId = TenantId,
                MembershipNumber = memberNumber,
                Email = model.Email.Trim().ToLowerInvariant(),
                NormalizedEmail = model.Email.Trim().ToUpperInvariant(),
                PasswordHash = password, // plain text for now
                PasswordSalt = null,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                HomeBranchId = model.HomeBranchId,
                MemberStatusId = statusId,
                ProfileImageUrl = imageUrl,
                EmergencyContactName = model.EmergencyContactName?.Trim(),
                EmergencyContactPhone = model.EmergencyContactPhone?.Trim(),
                Notes = model.Notes?.Trim(),
                MustChangePassword = true,
                IsActive = true,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Member {member.FirstName} {member.LastName} created. Membership # {memberNumber}. Temp password: {password}";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Details), new { id = memberId });
        }

        // ─────────────────────────────────────────────
        // GET /Members/Edit/id
        // ─────────────────────────────────────────────
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var m = await _db.Members
                .FirstOrDefaultAsync(x => x.MemberId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (m == null) return NotFound();

            var vm = new MemberFormViewModel
            {
                MemberId = m.MemberId,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Email = m.Email,
                PhoneNumber = m.PhoneNumber,
                HomeBranchId = m.HomeBranchId,
                DateOfBirth = m.DateOfBirth,
                Gender = m.Gender,
                EmergencyContactName = m.EmergencyContactName,
                EmergencyContactPhone = m.EmergencyContactPhone,
                Notes = m.Notes,
                ExistingImageUrl = m.ProfileImageUrl,
                Branches = await GetBranchesAsync(),
            };

            ViewData["Title"] = "Members";
            ViewData["Subtitle"] = "Edit Member";
            return View("CreateEdit", vm);
        }

        // ─────────────────────────────────────────────
        // POST /Members/Edit/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Edit(Guid id, MemberFormViewModel model)
        {
            var m = await _db.Members
                .FirstOrDefaultAsync(x => x.MemberId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (m == null) return NotFound();

            await ValidateUniqueFields(model, id);

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                model.ExistingImageUrl = m.ProfileImageUrl;
                ViewData["Title"] = "Members";
                ViewData["Subtitle"] = "Edit Member";
                return View("CreateEdit", model);
            }

            // Handle new profile image upload
            if (model.ProfileImage != null)
            {
                // Delete old image if exists
                DeleteProfileImage(m.ProfileImageUrl);
                m.ProfileImageUrl = await SaveProfileImageAsync(model.ProfileImage, id);
            }

            m.FirstName = model.FirstName.Trim();
            m.LastName = model.LastName.Trim();
            m.Email = model.Email.Trim().ToLowerInvariant();
            m.NormalizedEmail = model.Email.Trim().ToUpperInvariant();
            m.PhoneNumber = model.PhoneNumber.Trim();
            m.HomeBranchId = model.HomeBranchId;
            m.DateOfBirth = model.DateOfBirth;
            m.Gender = model.Gender;
            m.EmergencyContactName = model.EmergencyContactName?.Trim();
            m.EmergencyContactPhone = model.EmergencyContactPhone?.Trim();
            m.Notes = model.Notes?.Trim();
            m.UpdatedAtUtc = DateTime.UtcNow;
            m.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"{m.FirstName} {m.LastName}'s profile updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ─────────────────────────────────────────────
        // POST /Members/ChangeStatus
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> ChangeStatus(Guid id, string statusCode)
        {
            var m = await _db.Members
                .FirstOrDefaultAsync(x => x.MemberId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (m == null) return NotFound();

            var newStatus = await _db.MemberStatuses
                .FirstOrDefaultAsync(s => s.StatusCode == statusCode);

            if (newStatus == null) return BadRequest();

            m.MemberStatusId = newStatus.MemberStatusId;
            m.IsActive = statusCode == "ACTIVE";
            m.UpdatedAtUtc = DateTime.UtcNow;
            m.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Member status changed to {newStatus.StatusName}.";
            TempData["ToastType"] = statusCode == "ACTIVE" ? "success" : "warning";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ─────────────────────────────────────────────
        // POST /Members/Delete/id  (soft delete)
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var m = await _db.Members
                .FirstOrDefaultAsync(x => x.MemberId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (m == null) return NotFound();

            m.IsDeleted = true;
            m.IsActive = false;
            m.DeletedAtUtc = DateTime.UtcNow;
            m.DeletedByUserId = UserId;
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Member {m.FirstName} {m.LastName} has been removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────

        private async Task<List<BranchDropdownItem>> GetBranchesAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem { BranchId = b.BranchId, BranchName = b.BranchName })
                .ToListAsync();

        private async Task ValidateUniqueFields(MemberFormViewModel model, Guid? excludeId)
        {
            var emailNorm = model.Email.Trim().ToUpperInvariant();
            var phone = model.PhoneNumber.Trim();

            if (await _db.Members.AnyAsync(m =>
                m.TenantId == TenantId &&
                m.NormalizedEmail == emailNorm &&
                !m.IsDeleted &&
                (excludeId == null || m.MemberId != excludeId)))
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            }

            if (await _db.Members.AnyAsync(m =>
                m.TenantId == TenantId &&
                m.PhoneNumber == phone &&
                !m.IsDeleted &&
                (excludeId == null || m.MemberId != excludeId)))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "This phone number is already in use.");
            }
        }

        private async Task<string> GenerateMembershipNumberAsync(string firstName, string lastName)
        {
            // Format: IW-{first2letters}{last2letters}-{4digits}
            // e.g. IW-MAHM-4821
            var prefix = (firstName.Length >= 2 ? firstName[..2] : firstName).ToUpperInvariant();
            var suffix = (lastName.Length >= 2 ? lastName[..2] : lastName).ToUpperInvariant();
            var tag = $"IW-{prefix}{suffix}";

            string number;
            do
            {
                number = $"{tag}-{Random.Shared.Next(1000, 9999)}";
            }
            while (await _db.Members.AnyAsync(m => m.TenantId == TenantId && m.MembershipNumber == number));

            return number;
        }

        private static string GeneratePassword(string firstName)
        {
            // Format: first3letters + 4digits + ! e.g. Mah4821!
            var name = (firstName.Length >= 3 ? firstName[..3] : firstName);
            name = char.ToUpper(name[0]) + name[1..].ToLower();
            return $"{name}{Random.Shared.Next(1000, 9999)}!";
        }

        private async Task<string?> SaveProfileImageAsync(IFormFile? file, Guid memberId)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "members");
            Directory.CreateDirectory(folder);

            var fileName = $"{memberId}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/members/{fileName}";
        }

        private void DeleteProfileImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;
            var filePath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}