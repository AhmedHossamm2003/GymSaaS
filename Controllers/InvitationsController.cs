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
        public async Task<IActionResult> Cancel(Guid id, Guid memberId)
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
                return RedirectToAction("Details", "Members", new { id = memberId });
            }

            invitation.Status = "CANCELLED";

            // Restore the invitation count on the package
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
            return RedirectToAction("Details", "Members", new { id = memberId });
        }

        // ─────────────────────────────────────────────
        // POST /Invitations/MarkUsed/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AnyStaff")]
        public async Task<IActionResult> MarkUsed(Guid id, Guid memberId)
        {
            var invitation = await _db.MemberInvitations
                .FirstOrDefaultAsync(i => i.MemberInvitationId == id && i.TenantId == TenantId);

            if (invitation == null)
                return NotFound();

            if (invitation.Status != "PENDING")
            {
                TempData["Toast"] = "Only pending invitations can be marked as used.";
                TempData["ToastType"] = "error";
                return RedirectToAction("Details", "Members", new { id = memberId });
            }

            invitation.Status = "USED";
            invitation.UsedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Toast"] = "Invitation marked as used.";
            TempData["ToastType"] = "success";
            return RedirectToAction("Details", "Members", new { id = memberId });
        }
    }
}
