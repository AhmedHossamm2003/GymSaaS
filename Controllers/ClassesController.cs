using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "ManagerAndAbove")]
    public class ClassesController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ClassesController(GymDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET /Classes
        public async Task<IActionResult> Index(string? search, Guid? branchId, Guid? coachId, int? day)
        {
            var classes = await BuildQueryAsync(search, branchId, coachId, day);

            ViewData["Title"] = "Classes";
            ViewData["Search"] = search;
            ViewData["BranchId"] = branchId;
            ViewData["CoachId"] = coachId;
            ViewData["Day"] = day;
            ViewData["Branches"] = await GetBranchesViewDataAsync();
            ViewData["Coaches"] = await GetCoachesViewDataAsync();
            ViewData["TotalCount"] = classes.Count;
            ViewData["ActiveCount"] = classes.Count(c => c.IsActive);

            return View(classes);
        }

        // GET /Classes/Schedule
        public async Task<IActionResult> Schedule(Guid? branchId, Guid? coachId)
        {
            var classes = await BuildQueryAsync(null, branchId, coachId, null);

            ViewData["Title"] = "Classes";
            ViewData["Subtitle"] = "Weekly Schedule";
            ViewData["BranchId"] = branchId;
            ViewData["CoachId"] = coachId;
            ViewData["Branches"] = await GetBranchesViewDataAsync();
            ViewData["Coaches"] = await GetCoachesViewDataAsync();

            return View(classes);
        }

        // GET /Classes/Create
        public async Task<IActionResult> Create()
        {
            var vm = new ClassFormViewModel
            {
                Branches = await GetBranchesAsync(),
                Coaches = await GetCoachesAsync(),
            };
            ViewData["Title"] = "Classes";
            ViewData["Subtitle"] = "New Class";
            return View("CreateEdit", vm);
        }

        // POST /Classes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClassFormViewModel model)
        {
            ValidateTimes(model);

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                model.Coaches = await GetCoachesAsync();
                ViewData["Title"] = "Classes";
                ViewData["Subtitle"] = "New Class";
                return View("CreateEdit", model);
            }

            var classId = Guid.NewGuid();
            var photoUrl = await SavePhotoAsync(model.Photo, classId);

            var gymClass = new GymClass
            {
                GymClassId = classId,
                TenantId = TenantId,
                BranchId = model.BranchId,
                CoachId = model.CoachId,
                ClassName = model.ClassName.Trim(),
                Description = model.Description?.Trim(),
                DayOfWeek = model.DayOfWeek,
                StartTime = TimeOnly.Parse(model.StartTimeStr),
                EndTime = TimeOnly.Parse(model.EndTimeStr),
                Capacity = model.Capacity,
                PhotoUrl = photoUrl,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.GymClasses.Add(gymClass);
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Class '{gymClass.ClassName}' created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // GET /Classes/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var g = await _db.GymClasses
                .FirstOrDefaultAsync(x => x.GymClassId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (g == null) return NotFound();

            var vm = new ClassFormViewModel
            {
                GymClassId = g.GymClassId,
                ClassName = g.ClassName,
                Description = g.Description,
                CoachId = g.CoachId,
                BranchId = g.BranchId,
                DayOfWeek = g.DayOfWeek,
                StartTimeStr = g.StartTime.ToString("HH:mm"),
                EndTimeStr = g.EndTime.ToString("HH:mm"),
                Capacity = g.Capacity,
                ExistingPhotoUrl = g.PhotoUrl,
                IsActive = g.IsActive,
                Branches = await GetBranchesAsync(),
                Coaches = await GetCoachesAsync(),
            };

            ViewData["Title"] = "Classes";
            ViewData["Subtitle"] = "Edit Class";
            return View("CreateEdit", vm);
        }

        // POST /Classes/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ClassFormViewModel model)
        {
            var g = await _db.GymClasses
                .FirstOrDefaultAsync(x => x.GymClassId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (g == null) return NotFound();

            ValidateTimes(model);

            if (!ModelState.IsValid)
            {
                model.ExistingPhotoUrl = g.PhotoUrl;
                model.Branches = await GetBranchesAsync();
                model.Coaches = await GetCoachesAsync();
                ViewData["Title"] = "Classes";
                ViewData["Subtitle"] = "Edit Class";
                return View("CreateEdit", model);
            }

            if (model.Photo != null)
            {
                DeletePhoto(g.PhotoUrl);
                g.PhotoUrl = await SavePhotoAsync(model.Photo, id);
            }

            g.ClassName = model.ClassName.Trim();
            g.Description = model.Description?.Trim();
            g.CoachId = model.CoachId;
            g.BranchId = model.BranchId;
            g.DayOfWeek = model.DayOfWeek;
            g.StartTime = TimeOnly.Parse(model.StartTimeStr);
            g.EndTime = TimeOnly.Parse(model.EndTimeStr);
            g.Capacity = model.Capacity;
            g.IsActive = model.IsActive;
            g.UpdatedAtUtc = DateTime.UtcNow;
            g.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Class '{g.ClassName}' updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // POST /Classes/Delete/id (soft delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var g = await _db.GymClasses
                .FirstOrDefaultAsync(x => x.GymClassId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (g == null) return NotFound();

            g.IsDeleted = true;
            g.IsActive = false;
            g.UpdatedAtUtc = DateTime.UtcNow;
            g.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Class '{g.ClassName}' removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────

        private async Task<List<ClassListItem>> BuildQueryAsync(
            string? search, Guid? branchId, Guid? coachId, int? day)
        {
            var query = _db.GymClasses
                .Where(g => g.TenantId == TenantId && !g.IsDeleted)
                .Join(_db.Branches, g => g.BranchId, b => b.BranchId, (g, b) => new { g, b });

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.g.ClassName.Contains(search));

            if (branchId.HasValue)
                query = query.Where(x => x.g.BranchId == branchId.Value);

            if (day.HasValue)
                query = query.Where(x => x.g.DayOfWeek == day.Value);

            var raw = await query
                .OrderBy(x => x.g.DayOfWeek)
                .ThenBy(x => x.g.StartTime)
                .Select(x => new
                {
                    x.g.GymClassId,
                    x.g.ClassName,
                    x.g.Description,
                    x.g.CoachId,
                    x.g.BranchId,
                    x.g.DayOfWeek,
                    x.g.StartTime,
                    x.g.EndTime,
                    x.g.Capacity,
                    x.g.PhotoUrl,
                    x.g.IsActive,
                    x.g.CreatedAtUtc,
                    BranchName = x.b.BranchName,
                })
                .ToListAsync();

            if (coachId.HasValue)
                raw = raw.Where(x => x.CoachId == coachId.Value).ToList();

            var coachIds = raw.Where(x => x.CoachId.HasValue).Select(x => x.CoachId!.Value).Distinct().ToList();
            var coachNames = await _db.Coaches
                .Where(c => coachIds.Contains(c.CoachId))
                .Select(c => new { c.CoachId, FullName = c.FirstName + " " + c.LastName })
                .ToDictionaryAsync(x => x.CoachId, x => x.FullName.Trim());

            return raw.Select(x => new ClassListItem
            {
                GymClassId = x.GymClassId,
                ClassName = x.ClassName,
                Description = x.Description,
                CoachId = x.CoachId,
                CoachName = x.CoachId.HasValue && coachNames.TryGetValue(x.CoachId.Value, out var cn) ? cn : "—",
                BranchId = x.BranchId,
                BranchName = x.BranchName,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Capacity = x.Capacity,
                PhotoUrl = x.PhotoUrl,
                IsActive = x.IsActive,
                CreatedAtUtc = x.CreatedAtUtc,
            }).ToList();
        }

        private void ValidateTimes(ClassFormViewModel model)
        {
            if (!TimeOnly.TryParse(model.StartTimeStr, out var start))
                ModelState.AddModelError(nameof(model.StartTimeStr), "Invalid start time.");
            if (!TimeOnly.TryParse(model.EndTimeStr, out var end))
                ModelState.AddModelError(nameof(model.EndTimeStr), "Invalid end time.");
            if (ModelState.IsValid && end <= start)
                ModelState.AddModelError(nameof(model.EndTimeStr), "End time must be after start time.");
        }

        private async Task<List<BranchDropdownItem>> GetBranchesAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem { BranchId = b.BranchId, BranchName = b.BranchName })
                .ToListAsync();

        private async Task<List<CoachDropdownItem>> GetCoachesAsync() =>
            await _db.Coaches
                .Where(c => c.TenantId == TenantId && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.FirstName)
                .Select(c => new CoachDropdownItem
                {
                    CoachId = c.CoachId,
                    FullName = c.FirstName + " " + c.LastName,
                    Specialty = c.Specialty,
                    BranchId = c.BranchId,
                })
                .ToListAsync();

        private async Task<object> GetBranchesViewDataAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .ToListAsync();

        private async Task<object> GetCoachesViewDataAsync() =>
            await _db.Coaches
                .Where(c => c.TenantId == TenantId && !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.FirstName)
                .Select(c => new CoachDropdownItem
                {
                    CoachId = c.CoachId,
                    FullName = c.FirstName + " " + c.LastName,
                    Specialty = c.Specialty,
                    BranchId = c.BranchId,
                })
                .ToListAsync();

        private async Task<string?> SavePhotoAsync(IFormFile? file, Guid classId)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "classes");
            Directory.CreateDirectory(folder);

            var fileName = $"{classId}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/classes/{fileName}";
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
