using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace GymSaaS.Controllers
{
    [Authorize]
    public class InvitationsController : Controller
    {
        private readonly GymDbContext _db;

        public InvitationsController(GymDbContext db)
        {
            _db = db;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /Invitations/Index  — Invitations Attendance Log
        // ─────────────────────────────────────────────
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            const int pageSize = 25;
            var tenantId = TenantId;

            var query = _db.MemberInvitations
                .Where(i => i.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(i =>
                    i.GuestName.Contains(s) ||
                    i.GuestPhone.Contains(s) ||
                    i.Member.FirstName.Contains(s) ||
                    i.Member.LastName.Contains(s) ||
                    i.Member.MembershipNumber.Contains(s));
            }

            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var items = await query
                .OrderByDescending(i => i.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new InvitationAttendanceItem
                {
                    MemberInvitationId     = i.MemberInvitationId,
                    GuestName              = i.GuestName,
                    GuestPhone             = i.GuestPhone,
                    InvitedMemberName      = i.InvitedMember != null ? i.InvitedMember.FullName : null,
                    InviterName            = i.Member.FullName ?? (i.Member.FirstName + " " + i.Member.LastName),
                    InviterMembershipNumber = i.Member.MembershipNumber,
                    InviterMemberId        = i.MemberId,
                    PackageName            = i.MemberPackage.PackageNameSnapshot,
                    Status                 = i.Status,
                    Notes                  = i.Notes,
                    CreatedAtUtc           = i.CreatedAtUtc,
                    UsedAtUtc              = i.UsedAtUtc,
                })
                .ToListAsync();

            // Summary stats for header
            var allStatuses = await _db.MemberInvitations
                .Where(i => i.TenantId == tenantId)
                .GroupBy(i => i.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            ViewData["Title"]       = "Invitations";
            ViewData["Subtitle"]    = "Guest Attendance Log";
            ViewData["Status"]      = status;
            ViewData["Search"]      = search;
            ViewData["Page"]        = page;
            ViewData["TotalPages"]  = totalPages;
            ViewData["TotalCount"]  = total;
            ViewData["TotalPending"]   = allStatuses.FirstOrDefault(x => x.Key == "PENDING")?.Count ?? 0;
            ViewData["TotalUsed"]      = allStatuses.FirstOrDefault(x => x.Key == "USED")?.Count ?? 0;
            ViewData["TotalCancelled"] = allStatuses.FirstOrDefault(x => x.Key == "CANCELLED")?.Count ?? 0;

            return View(items);
        }

        // ─────────────────────────────────────────────
        // GET /Invitations/MemberLookup?q=...
        // AJAX — used by the Reception invitation widget
        // Finds a member and checks invitation slots
        // ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> MemberLookup(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
                return Json(new { found = false });

            var term = q.Trim();
            var tenantId = TenantId;

            var member = await _db.Members
                .Where(m => m.TenantId == tenantId && !m.IsDeleted &&
                            (m.MembershipNumber == term || m.PhoneNumber == term))
                .Select(m => new
                {
                    m.MemberId,
                    m.FirstName,
                    m.LastName,
                    m.FullName,
                    m.MembershipNumber,
                    m.ProfileImageUrl,
                })
                .FirstOrDefaultAsync();

            if (member == null)
                return Json(new { found = false });

            var pkg = await _db.MemberPackages
                .Where(p => p.MemberId == member.MemberId && p.Status == "ACTIVE" && (p.InvitationsRemaining ?? 0) > 0)
                .OrderBy(p => p.ValidToDate)
                .Select(p => new { p.MemberPackageId, p.PackageNameSnapshot, p.InvitationsRemaining })
                .FirstOrDefaultAsync();

            return Json(new
            {
                found                = true,
                memberId             = member.MemberId,
                name                 = member.FullName ?? $"{member.FirstName} {member.LastName}".Trim(),
                membershipNumber     = member.MembershipNumber,
                photoUrl             = member.ProfileImageUrl,
                hasInvitations       = pkg != null,
                invitationsRemaining = pkg?.InvitationsRemaining ?? 0,
                packageName          = pkg?.PackageNameSnapshot,
            });
        }

        // ─────────────────────────────────────────────
        // POST /Invitations/CreateAtReception  (AJAX)
        // Used by the Reception page invitation widget
        // Returns JSON (no page redirect)
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> CreateAtReception([FromBody] CreateInvitationViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Please fill all required fields." });

            var tenantId = TenantId;

            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == model.MemberId && m.TenantId == tenantId && !m.IsDeleted);

            if (member == null)
                return BadRequest(new { success = false, message = "Member not found." });

            var pkg = await _db.MemberPackages
                .Where(p => p.MemberId == model.MemberId && p.Status == "ACTIVE" && (p.InvitationsRemaining ?? 0) > 0)
                .OrderBy(p => p.ValidToDate)
                .FirstOrDefaultAsync();

            if (pkg == null)
                return Ok(new { success = false, message = "No active package with remaining invitations." });

            Guid? confirmedInvitedMemberId = null;
            if (model.InvitedMemberId.HasValue)
            {
                var exists = await _db.Members.AnyAsync(m =>
                    m.MemberId == model.InvitedMemberId.Value && m.TenantId == tenantId && !m.IsDeleted);
                if (exists)
                    confirmedInvitedMemberId = model.InvitedMemberId.Value;
            }

            var invitation = new MemberInvitation
            {
                TenantId            = tenantId,
                MemberId            = model.MemberId,
                MemberPackageId     = pkg.MemberPackageId,
                GuestName           = model.GuestName.Trim(),
                GuestPhone          = model.GuestPhone.Trim(),
                InvitedMemberId     = confirmedInvitedMemberId,
                Status              = "PENDING",
                Notes               = model.Notes?.Trim(),
                CreatedAtUtc        = DateTime.UtcNow,
                CreatedByUserId     = UserId,
            };

            _db.MemberInvitations.Add(invitation);
            pkg.InvitationsRemaining = (pkg.InvitationsRemaining ?? 0) - 1;
            pkg.UpdatedAtUtc         = DateTime.UtcNow;
            pkg.UpdatedByUserId      = UserId;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success              = true,
                message              = $"Invitation recorded for {invitation.GuestName}.",
                invitationsRemaining = pkg.InvitationsRemaining,
                guestName            = invitation.GuestName,
            });
        }

        // ─────────────────────────────────────────────
        // GET /Invitations/SearchMember?phone=xxx
        // AJAX — returns JSON with member info if found
        // ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> SearchMember(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return Json(new { found = false });

            var member = await _db.Members
                .Where(m => m.TenantId == TenantId && m.PhoneNumber == phone.Trim() && !m.IsDeleted)
                .Select(m => new { m.MemberId, m.FirstName, m.LastName, m.FullName })
                .FirstOrDefaultAsync();

            if (member == null)
                return Json(new { found = false });

            return Json(new
            {
                found = true,
                memberId = member.MemberId,
                name = member.FullName ?? $"{member.FirstName} {member.LastName}".Trim()
            });
        }

        // ─────────────────────────────────────────────
        // POST /Invitations/Create
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Create(CreateInvitationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Toast"] = "Please fill all required fields.";
                TempData["ToastType"] = "error";
                return RedirectToAction("Details", "Members", new { id = model.MemberId });
            }

            // Verify member belongs to this tenant
            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == model.MemberId && m.TenantId == TenantId && !m.IsDeleted);

            if (member == null)
                return NotFound();

            // Find the first active package with invitations remaining
            var pkg = await _db.MemberPackages
                .Where(p => p.MemberId == model.MemberId && p.Status == "ACTIVE" && (p.InvitationsRemaining ?? 0) > 0)
                .OrderBy(p => p.ValidToDate)
                .FirstOrDefaultAsync();

            if (pkg == null)
            {
                TempData["Toast"] = "No active package with remaining invitations found.";
                TempData["ToastType"] = "error";
                return RedirectToAction("Details", "Members", new { id = model.MemberId });
            }

            // Verify invitedMemberId belongs to same tenant if provided
            Guid? confirmedInvitedMemberId = null;
            if (model.InvitedMemberId.HasValue)
            {
                var exists = await _db.Members.AnyAsync(m =>
                    m.MemberId == model.InvitedMemberId.Value &&
                    m.TenantId == TenantId &&
                    !m.IsDeleted);

                if (exists)
                    confirmedInvitedMemberId = model.InvitedMemberId.Value;
            }

            var invitation = new MemberInvitation
            {
                TenantId = TenantId,
                MemberId = model.MemberId,
                MemberPackageId = pkg.MemberPackageId,
                GuestName = model.GuestName.Trim(),
                GuestPhone = model.GuestPhone.Trim(),
                InvitedMemberId = confirmedInvitedMemberId,
                Status = "PENDING",
                Notes = model.Notes?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.MemberInvitations.Add(invitation);

            // Decrement the remaining count
            pkg.InvitationsRemaining = (pkg.InvitationsRemaining ?? 0) - 1;
            pkg.UpdatedAtUtc = DateTime.UtcNow;
            pkg.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Invitation recorded for {invitation.GuestName}.";
            TempData["ToastType"] = "success";
            return RedirectToAction("Details", "Members", new { id = model.MemberId });
        }

        // ─────────────────────────────────────────────
        // POST /Invitations/Cancel/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> Cancel(Guid id, Guid memberId, bool returnToIndex = false)
        {
            var invitation = await _db.MemberInvitations
                .Include(i => i.MemberPackage)
                .FirstOrDefaultAsync(i => i.MemberInvitationId == id && i.TenantId == TenantId);

            if (invitation == null)
                return NotFound();

            if (invitation.Status != "PENDING")
            {
                TempData["Toast"] = "Only pending invitations can be cancelled.";
                TempData["ToastType"] = "error";
                return returnToIndex
                    ? RedirectToAction("Index")
                    : RedirectToAction("Details", "Members", new { id = memberId });
            }

            invitation.Status = "CANCELLED";

            var pkg = invitation.MemberPackage;
            if (pkg != null && pkg.Status == "ACTIVE")
            {
                pkg.InvitationsRemaining = (pkg.InvitationsRemaining ?? 0) + 1;
                pkg.UpdatedAtUtc = DateTime.UtcNow;
                pkg.UpdatedByUserId = UserId;
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = "Invitation cancelled.";
            TempData["ToastType"] = "warning";
            return returnToIndex
                ? RedirectToAction("Index")
                : RedirectToAction("Details", "Members", new { id = memberId });
        }

        // ─────────────────────────────────────────────
        // POST /Invitations/MarkUsed/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> MarkUsed(Guid id, Guid memberId, bool returnToIndex = false)
        {
            var invitation = await _db.MemberInvitations
                .FirstOrDefaultAsync(i => i.MemberInvitationId == id && i.TenantId == TenantId);

            if (invitation == null)
                return NotFound();

            if (invitation.Status != "PENDING")
            {
                TempData["Toast"] = "Only pending invitations can be marked as used.";
                TempData["ToastType"] = "error";
                return returnToIndex
                    ? RedirectToAction("Index")
                    : RedirectToAction("Details", "Members", new { id = memberId });
            }

            invitation.Status = "USED";
            invitation.UsedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Toast"] = "Invitation marked as used.";
            TempData["ToastType"] = "success";
            return returnToIndex
                ? RedirectToAction("Index")
                : RedirectToAction("Details", "Members", new { id = memberId });
        }
    }
}
