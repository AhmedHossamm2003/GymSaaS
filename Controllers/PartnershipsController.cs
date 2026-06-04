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
    public class PartnershipsController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IWebHostEnvironment _env;

        public PartnershipsController(GymDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET /Partnerships
        public async Task<IActionResult> Index(string? search, bool? activeOnly)
        {
            var query = _db.Partnerships
                .Where(p => p.TenantId == TenantId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.Contains(search));

            if (activeOnly == true)
                query = query.Where(p => p.IsActive);

            var raw = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => new
                {
                    p.PartnershipId,
                    p.Name,
                    p.Description,
                    p.LogoImageUrl,
                    p.DiscountPercentage,
                    p.IsActive,
                    p.CreatedAtUtc,
                })
                .ToListAsync();

            var ids = raw.Select(p => p.PartnershipId).ToList();
            var memberCounts = await _db.MemberPartnerships
                .Where(mp => ids.Contains(mp.PartnershipId))
                .GroupBy(mp => mp.PartnershipId)
                .Select(g => new { PartnershipId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PartnershipId, x => x.Count);

            var items = raw.Select(p => new PartnershipListItem
            {
                PartnershipId     = p.PartnershipId,
                Name              = p.Name,
                Description       = p.Description,
                LogoImageUrl      = p.LogoImageUrl,
                DiscountPercentage = p.DiscountPercentage,
                IsActive          = p.IsActive,
                MemberCount       = memberCounts.TryGetValue(p.PartnershipId, out var c) ? c : 0,
                CreatedAtUtc      = p.CreatedAtUtc,
            }).ToList();

            ViewData["Title"]      = "Partnerships";
            ViewData["Search"]     = search;
            ViewData["ActiveOnly"] = activeOnly;
            ViewData["TotalCount"] = items.Count;

            return View(items);
        }

        // GET /Partnerships/Create
        public IActionResult Create()
        {
            ViewData["Title"]    = "Partnerships";
            ViewData["Subtitle"] = "New Partnership";
            return View("CreateEdit", new PartnershipFormViewModel());
        }

        // POST /Partnerships/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PartnershipFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Title"]    = "Partnerships";
                ViewData["Subtitle"] = "New Partnership";
                return View("CreateEdit", model);
            }

            var partnershipId = Guid.NewGuid();
            var logoUrl = await SaveLogoAsync(model.Logo, partnershipId);

            var entity = new Partnership
            {
                PartnershipId      = partnershipId,
                TenantId           = TenantId,
                Name               = model.Name.Trim(),
                Description        = model.Description?.Trim(),
                LogoImageUrl       = logoUrl,
                DiscountPercentage = model.DiscountPercentage,
                IsActive           = model.IsActive,
                CreatedAtUtc       = DateTime.UtcNow,
                CreatedByUserId    = UserId,
            };

            _db.Partnerships.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Toast"]     = $"Partnership \"{entity.Name}\" created.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // GET /Partnerships/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var p = await _db.Partnerships
                .FirstOrDefaultAsync(x => x.PartnershipId == id && x.TenantId == TenantId);

            if (p == null) return NotFound();

            var vm = new PartnershipFormViewModel
            {
                PartnershipId      = p.PartnershipId,
                Name               = p.Name,
                Description        = p.Description,
                DiscountPercentage = p.DiscountPercentage,
                IsActive           = p.IsActive,
                ExistingLogoUrl    = p.LogoImageUrl,
            };

            ViewData["Title"]    = "Partnerships";
            ViewData["Subtitle"] = "Edit Partnership";
            return View("CreateEdit", vm);
        }

        // POST /Partnerships/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, PartnershipFormViewModel model)
        {
            var p = await _db.Partnerships
                .FirstOrDefaultAsync(x => x.PartnershipId == id && x.TenantId == TenantId);

            if (p == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.ExistingLogoUrl = p.LogoImageUrl;
                ViewData["Title"]    = "Partnerships";
                ViewData["Subtitle"] = "Edit Partnership";
                return View("CreateEdit", model);
            }

            if (model.Logo != null)
            {
                DeleteLogo(p.LogoImageUrl);
                p.LogoImageUrl = await SaveLogoAsync(model.Logo, id);
            }

            p.Name               = model.Name.Trim();
            p.Description        = model.Description?.Trim();
            p.DiscountPercentage = model.DiscountPercentage;
            p.IsActive           = model.IsActive;
            p.UpdatedAtUtc       = DateTime.UtcNow;
            p.UpdatedByUserId    = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"]     = $"\"{p.Name}\" updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // POST /Partnerships/Delete/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var p = await _db.Partnerships
                .FirstOrDefaultAsync(x => x.PartnershipId == id && x.TenantId == TenantId);

            if (p == null) return NotFound();

            DeleteLogo(p.LogoImageUrl);
            _db.Partnerships.Remove(p);
            await _db.SaveChangesAsync();

            TempData["Toast"]     = $"Partnership \"{p.Name}\" deleted.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // GET /Partnerships/Search?q=  (AJAX — used by member details page)
        [HttpGet]
        public async Task<IActionResult> Search(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
                return Json(Array.Empty<object>());

            var term = q.Trim();

            var results = await _db.Partnerships
                .Where(p => p.TenantId == TenantId && p.IsActive && p.Name.Contains(term))
                .Take(10)
                .Select(p => new { p.PartnershipId, p.Name, p.LogoImageUrl, p.DiscountPercentage })
                .ToListAsync();

            return Json(results);
        }

        // ── Helpers ──────────────────────────────────

        private async Task<string?> SaveLogoAsync(IFormFile? file, Guid partnershipId)
        {
            if (file == null || file.Length == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "partnerships");
            Directory.CreateDirectory(folder);

            var fileName = $"{partnershipId}{ext}";
            var filePath = Path.Combine(folder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/partnerships/{fileName}";
        }

        private void DeleteLogo(string? logoUrl)
        {
            if (string.IsNullOrEmpty(logoUrl)) return;
            var filePath = Path.Combine(_env.WebRootPath, logoUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}
