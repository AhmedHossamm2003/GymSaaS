// ================================================================
// Controllers/ReceptionController.cs
// ================================================================

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymSaaS.Services.Reception;

namespace GymSaaS.Controllers
{
    [Authorize]
    public class ReceptionController : Controller
    {
        private readonly IReceptionService _receptionService;

        public ReceptionController(IReceptionService receptionService)
        {
            _receptionService = receptionService;
        }

        // ── Helpers ───────────────────────────────────────────────
        private Guid CurrentTenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string CurrentRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        private bool IsAdminOrSuper =>
            CurrentRole is "SuperAdmin" or "Admin";

        // ── GET /Reception/Index ──────────────────────────────────
        /// <summary>
        /// Main reception page.
        /// Admin/SuperAdmin: show branch selector, load first branch by default.
        /// Other users: load their assigned branch directly.
        /// </summary>
        public async Task<IActionResult> Index(Guid? branchId)
        {
            var tenantId = CurrentTenantId;

            if (IsAdminOrSuper)
            {
                // Admins can pick any branch
                var branches = await _receptionService.GetBranchesAsync(tenantId);
                ViewBag.Branches   = branches;
                ViewBag.IsAdmin    = true;

                // Default to first branch or the one passed in URL
                var selectedId = branchId ?? (branches.Count > 0 ? branches[0].BranchId : (Guid?)null);
                ViewBag.SelectedBranchId = selectedId;

                if (selectedId == null)
                {
                    ViewBag.Dashboard = null;
                    return View();
                }

                var dashboard = await _receptionService.GetDashboardAsync(selectedId.Value, tenantId);
                ViewBag.Dashboard = dashboard;
            }
            else
            {
                // Reception/other users — get their assigned branch from claims
                var userBranchId = User.FindFirstValue("BranchId");
                if (userBranchId == null)
                    return Forbid();

                var assignedBranchId = Guid.Parse(userBranchId);
                ViewBag.IsAdmin          = false;
                ViewBag.SelectedBranchId = assignedBranchId;

                var dashboard = await _receptionService.GetDashboardAsync(assignedBranchId, tenantId);
                ViewBag.Dashboard = dashboard;
            }

            return View();
        }

        // ── POST /Reception/Scan  (AJAX) ──────────────────────────
        /// <summary>
        /// Simulates QR scan: receptionist types membership number.
        /// Returns JSON used to show/populate the popup modal.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Scan([FromBody] ScanRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.MembershipNumber) || model.BranchId == Guid.Empty)
                return BadRequest(new { success = false, errorMessage = "Invalid request." });

            var tenantId = CurrentTenantId;
            var result   = await _receptionService.ProcessScanAsync(
                model.MembershipNumber.Trim(), model.BranchId, tenantId);

            return Ok(result);
        }

        // ── GET /Reception/Stats  (AJAX) ─────────────────────────
        /// <summary>
        /// Returns live stats for a branch — called after each check-in to refresh counters.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Stats(Guid branchId)
        {
            if (branchId == Guid.Empty)
                return BadRequest();

            var tenantId = CurrentTenantId;
            var dashboard = await _receptionService.GetDashboardAsync(branchId, tenantId);

            if (dashboard == null)
                return NotFound();

            return Ok(new
            {
                currentlyPresentCount = dashboard.CurrentlyPresentCount,
                todayEntryCount       = dashboard.TodayEntryCount
            });
        }

        // ── POST /Reception/MarkAttendance  (AJAX) ────────────────
        /// <summary>
        /// Called when receptionist picks a package in the conflict popup.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest model)
        {
            if (model.MemberId == Guid.Empty || model.BranchId == Guid.Empty ||
                model.SelectedMemberPackageId == Guid.Empty)
                return BadRequest(new { success = false, errorMessage = "Invalid request." });

            model.ReceptionistUserId = CurrentUserId;
            var tenantId = CurrentTenantId;

            var result = await _receptionService.MarkAttendanceAsync(model, tenantId);
            return Ok(result);
        }
    }

    // ── Request model (bound from JSON body) ──────────────────────
    public class ScanRequestModel
    {
        public string MembershipNumber { get; set; } = null!;
        public Guid BranchId { get; set; }
    }
}
