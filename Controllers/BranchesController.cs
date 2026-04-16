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
    public class BranchesController : Controller
    {
        private readonly GymDbContext _db;
        private readonly QrCodeService _qrService;

        public BranchesController(GymDbContext db, QrCodeService qrService)
        {
            _db = db;
            _qrService = qrService;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /Branches
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index(string? search)
        {
            var query = _db.Branches.Where(b => b.TenantId == TenantId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(b =>
                    b.BranchName.Contains(search) ||
                    b.BranchCode.Contains(search) ||
                    (b.City != null && b.City.Contains(search)));

            var now = DateTime.UtcNow;

            var branches = await query
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchListItem
                {
                    BranchId = b.BranchId,
                    BranchCode = b.BranchCode,
                    BranchName = b.BranchName,
                    City = b.City ?? "—",
                    Country = b.Country ?? "—",
                    ContactPhone = b.ContactPhone ?? "—",
                    ContactEmail = b.ContactEmail ?? "—",
                    IsActive = b.IsActive,
                    CreatedAtUtc = b.CreatedAtUtc,
                    HasQrCode = _db.BranchQrcodes.Any(q => q.BranchId == b.BranchId && q.IsActive),
                    MemberCount = _db.Members.Count(m => m.HomeBranchId == b.BranchId && !m.IsDeleted),
                    TodayCheckins = _db.AttendanceRecords.Count(a => a.BranchId == b.BranchId && a.CheckInAtUtc.Date == now.Date),
                    InsideNow = _db.AttendanceRecords.Count(a => a.BranchId == b.BranchId && a.PresenceUntilUtc > now),
                })
                .ToListAsync();

            ViewData["Title"] = "Branches";
            ViewData["Search"] = search;
            return View(branches);
        }

        // ─────────────────────────────────────────────
        // GET /Branches/Details/id
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Details(Guid id)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            var now = DateTime.UtcNow;
            var qr = await _qrService.GetActiveQrAsync(id);

            var vm = new BranchDetailsViewModel
            {
                BranchId = b.BranchId,
                BranchCode = b.BranchCode,
                BranchName = b.BranchName,
                AddressLine1 = b.AddressLine1,
                AddressLine2 = b.AddressLine2,
                City = b.City,
                StateProvince = b.StateProvince,
                Country = b.Country,
                ContactPhone = b.ContactPhone,
                ContactEmail = b.ContactEmail,
                IsActive = b.IsActive,
                MemberPresenceWindowMinutes = b.MemberPresenceWindowMinutes,
                CreatedAtUtc = b.CreatedAtUtc,
                UpdatedAtUtc = b.UpdatedAtUtc,
                MemberCount = await _db.Members.CountAsync(m => m.HomeBranchId == id && !m.IsDeleted),
                TodayCheckins = await _db.AttendanceRecords.CountAsync(a => a.BranchId == id && a.CheckInAtUtc.Date == now.Date),
                InsideNow = await _db.AttendanceRecords.CountAsync(a => a.BranchId == id && a.PresenceUntilUtc > now),
                QrCodeValue = qr?.QrCodeValue,
                QrVersionNo = qr?.VersionNo,
                QrGeneratedAt = qr?.CreatedAtUtc,
                QrIsActive = qr?.IsActive ?? false,
            };

            ViewData["Title"] = b.BranchName;
            ViewData["Subtitle"] = "Branch Details";
            return View(vm);
        }

        // ─────────────────────────────────────────────
        // GET /Branches/Create
        // ─────────────────────────────────────────────
        [Authorize(Policy = "AdminAndAbove")]
        public IActionResult Create()
        {
            ViewData["Title"] = "Branches";
            ViewData["Subtitle"] = "New Branch";
            return View("CreateEdit", new BranchFormViewModel());
        }

        // ─────────────────────────────────────────────
        // POST /Branches/Create
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> Create(BranchFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Branches";
                ViewData["Subtitle"] = "New Branch";
                return View("CreateEdit", model);
            }

            // Auto-generate branch code: BR-{3 letters from name}-{4 random digits}
            var nameSlug = new string(
                model.BranchName.ToUpperInvariant()
                     .Where(char.IsLetter)
                     .Take(3)
                     .ToArray()
            ).PadRight(3, 'X');

            var autoCode = $"BR-{nameSlug}-{Random.Shared.Next(1000, 9999)}";
            while (await _db.Branches.AnyAsync(b => b.TenantId == TenantId && b.BranchCode == autoCode))
                autoCode = $"BR-{nameSlug}-{Random.Shared.Next(1000, 9999)}";

            var branch = new Branch
            {
                BranchId = Guid.NewGuid(),
                TenantId = TenantId,
                BranchCode = autoCode,
                BranchName = model.BranchName.Trim(),
                AddressLine1 = model.AddressLine1?.Trim(),
                AddressLine2 = model.AddressLine2?.Trim(),
                City = model.City?.Trim(),
                StateProvince = model.StateProvince?.Trim(),
                Country = model.Country?.Trim(),
                ContactPhone = model.ContactPhone?.Trim(),
                ContactEmail = model.ContactEmail?.Trim(),
                IsActive = model.IsActive,
                MemberPresenceWindowMinutes = model.MemberPresenceWindowMinutes,
                CurrentQrVersion = 1,
                CreatedAtUtc = DateTime.UtcNow,
            };

            _db.Branches.Add(branch);
            await _db.SaveChangesAsync();

            // Auto-generate QR code for the new branch
            await _qrService.GenerateForBranchAsync(TenantId, branch.BranchId, UserId, 1);

            TempData["Toast"] = $"Branch \"{branch.BranchName}\" created with code {branch.BranchCode}. QR code generated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Details), new { id = branch.BranchId });
        }

        // ─────────────────────────────────────────────
        // GET /Branches/Edit/id
        // ─────────────────────────────────────────────
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            var vm = new BranchFormViewModel
            {
                BranchId = b.BranchId,
                BranchCode = b.BranchCode,
                BranchName = b.BranchName,
                AddressLine1 = b.AddressLine1,
                AddressLine2 = b.AddressLine2,
                City = b.City,
                StateProvince = b.StateProvince,
                Country = b.Country,
                ContactPhone = b.ContactPhone,
                ContactEmail = b.ContactEmail,
                IsActive = b.IsActive,
                MemberPresenceWindowMinutes = b.MemberPresenceWindowMinutes,
            };

            ViewData["Title"] = "Branches";
            ViewData["Subtitle"] = "Edit Branch";
            return View("CreateEdit", vm);
        }

        // ─────────────────────────────────────────────
        // POST /Branches/Edit/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> Edit(Guid id, BranchFormViewModel model)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Branches";
                ViewData["Subtitle"] = "Edit Branch";
                return View("CreateEdit", model);
            }

            b.BranchName = model.BranchName.Trim();
            b.AddressLine1 = model.AddressLine1?.Trim();
            b.AddressLine2 = model.AddressLine2?.Trim();
            b.City = model.City?.Trim();
            b.StateProvince = model.StateProvince?.Trim();
            b.Country = model.Country?.Trim();
            b.ContactPhone = model.ContactPhone?.Trim();
            b.ContactEmail = model.ContactEmail?.Trim();
            b.IsActive = model.IsActive;
            b.MemberPresenceWindowMinutes = model.MemberPresenceWindowMinutes;
            b.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Branch \"{b.BranchName}\" updated successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Details), new { id = b.BranchId });
        }

        // ─────────────────────────────────────────────
        // POST /Branches/ToggleStatus/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            b.IsActive = !b.IsActive;
            b.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Branch \"{b.BranchName}\" {(b.IsActive ? "activated" : "deactivated")}.";
            TempData["ToastType"] = b.IsActive ? "success" : "warning";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // POST /Branches/RegenerateQr/id
        // Deactivates old QR, generates new one with next version number
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> RegenerateQr(Guid id)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            var qr = await _qrService.RegenerateAsync(TenantId, id, UserId);

            // Update branch's CurrentQrVersion
            b.CurrentQrVersion = qr.VersionNo;
            b.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Toast"] = $"QR code for \"{b.BranchName}\" regenerated (v{qr.VersionNo}). Old QR is now invalid.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ─────────────────────────────────────────────
        // GET /Branches/QrImage/id
        // Returns the QR as a PNG image for display + download
        // ─────────────────────────────────────────────
        public async Task<IActionResult> QrImage(Guid id)
        {
            var b = await _db.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id && x.TenantId == TenantId);

            if (b == null) return NotFound();

            var qr = await _qrService.GetActiveQrAsync(id);
            if (qr == null) return NotFound();

            // Generate QR PNG using QRCoder library
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(qr.QrCodeValue, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            var qrBytes = qrCode.GetGraphic(10);

            return File(qrBytes, "image/png", $"QR-{b.BranchCode}-v{qr.VersionNo}.png");
        }
    }
}