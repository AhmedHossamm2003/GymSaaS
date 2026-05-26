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

            if (branchId.HasValue)
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
            }).ToList();

            ViewData["Title"] = "Coaches";
            ViewData["Search"] = search;
            ViewData["BranchId"] = branchId;
            ViewData["ActiveOnly"] = activeOnly;
            ViewData["Branches"] = await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .ToListAsync();
            ViewData["TotalCount"] = coaches.Count;
            ViewData["ActiveCount"] = coaches.Count(c => c.IsActive);

            return View(coaches);
        }

        // GET /Coaches/Create
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoachFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"] = "Coaches";
                ViewData["Subtitle"] = "New Coach";
                return View("CreateEdit", model);
            }

            var coachId = Guid.NewGuid();
            var photoUrl = await SavePhotoAsync(model.Photo, coachId);

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
                Email = model.Email?.Trim().ToLowerInvariant(),
                PhotoUrl = photoUrl,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.Coaches.Add(coach);
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Coach {coach.FirstName} {coach.LastName} created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // GET /Coaches/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var c = await _db.Coaches
                .FirstOrDefaultAsync(x => x.CoachId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (c == null) return NotFound();

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
                ExistingPhotoUrl = c.PhotoUrl,
                IsActive = c.IsActive,
                Branches = await GetBranchesAsync(),
            };

            ViewData["Title"] = "Coaches";
            ViewData["Subtitle"] = "Edit Coach";
            return View("CreateEdit", vm);
        }

        // POST /Coaches/Edit/id
        [HttpPost]
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
