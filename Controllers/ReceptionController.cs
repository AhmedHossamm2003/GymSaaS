// ================================================================
// Controllers/ReceptionController.cs
// ================================================================

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GymSaaS.Services.Reception;
using GymSaaS.Services;
using GymSaaS.Models;

namespace GymSaaS.Controllers
{
    [GymSaaS.Authorization.ViewPermissionAuthorize]
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

        // ── Paid drop-in desk ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> DropIn(Guid? branchId, string? phone)
        {
            var branches = await GetAccessibleBranchesAsync();
            var selectedBranchId = branchId ?? branches.FirstOrDefault()?.BranchId;
            if (!selectedBranchId.HasValue) return NotFound("No active branch is available.");
            if (!CanUseBranch(selectedBranchId.Value)) return Forbid();

            var vm = await _receptionService.BuildDropInPageAsync(
                selectedBranchId.Value, CurrentTenantId, phone, IsAdminOrSuper);
            if (vm == null) return NotFound();
            vm.Branches = branches;
            ViewData["Title"] = "Paid Drop-In";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDropIn(DropInCheckoutViewModel model)
        {
            if (!CanUseBranch(model.BranchId)) return Forbid();

            if (model.MemberId == null)
            {
                if (string.IsNullOrWhiteSpace(model.FirstName))
                    ModelState.AddModelError(nameof(model.FirstName), "First name is required for a new member.");
                if (string.IsNullOrWhiteSpace(model.LastName))
                    ModelState.AddModelError(nameof(model.LastName), "Last name is required for a new member.");
                if (string.IsNullOrWhiteSpace(model.Email))
                    ModelState.AddModelError(nameof(model.Email), "Email is required for a new member.");
            }

            if (!ModelState.IsValid)
                return await RenderDropInErrorAsync(model, "Please correct the highlighted information.");

            var result = await _receptionService.CheckoutDropInAsync(
                model, CurrentUserId, CurrentTenantId);
            if (!result.Success)
                return await RenderDropInErrorAsync(model, result.ErrorMessage ?? "The drop-in could not be recorded.");

            TempData["Toast"] = result.MemberCreated
                ? $"{result.MemberName} was created and checked in. Membership # {result.MembershipNumber}. Paid {result.Amount:N2} EGP."
                : $"{result.MemberName} checked in. Paid {result.Amount:N2} EGP.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(DropIn), new { branchId = model.BranchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidDropIn(Guid incomeEntryId, Guid branchId, string reason)
        {
            if (!CanUseBranch(branchId)) return Forbid();
            var result = await _receptionService.VoidDropInAsync(
                incomeEntryId, branchId, reason, CurrentUserId, CurrentTenantId);
            TempData["Toast"] = result.Success
                ? "Drop-in payment was cancelled and its attendance was removed."
                : result.Error ?? "The drop-in could not be cancelled.";
            TempData["ToastType"] = result.Success ? "warning" : "danger";
            return RedirectToAction(nameof(DropIn), new { branchId });
        }

        [HttpGet]
        [Authorize(Policy = "AdminAndAbove")]
        public async Task<IActionResult> DropInSettings()
        {
            var page = await _receptionService.BuildDropInPageAsync(
                (await _receptionService.GetBranchesAsync(CurrentTenantId)).FirstOrDefault()?.BranchId ?? Guid.Empty,
                CurrentTenantId, null, true);
            if (page == null) return NotFound();
            ViewData["Title"] = "Drop-In Prices";
            return View(new DropInPriceSettingsViewModel
            {
                OpenGymPrice = page.OpenGymPrice,
                ClassPassPrice = page.ClassPassPrice,
            });
        }

        [HttpPost]
        [Authorize(Policy = "AdminAndAbove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DropInSettings(DropInPriceSettingsViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = await _receptionService.UpdateDropInPricesAsync(model, CurrentTenantId);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Prices could not be saved.");
                return View(model);
            }

            TempData["Toast"] = "Business-wide drop-in prices updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> RenderDropInErrorAsync(
            DropInCheckoutViewModel model, string error)
        {
            ModelState.AddModelError(string.Empty, error);
            var vm = await _receptionService.BuildDropInPageAsync(
                model.BranchId, CurrentTenantId, model.PhoneNumber, IsAdminOrSuper);
            if (vm == null) return NotFound();
            vm.Branches = await GetAccessibleBranchesAsync();
            vm.Checkout = model;
            ViewData["Title"] = "Paid Drop-In";
            return View("DropIn", vm);
        }

        private bool CanUseBranch(Guid branchId) =>
            IsAdminOrSuper || User.CanAccessBranch(branchId);

        private async Task<List<BranchOptionDto>> GetAccessibleBranchesAsync()
        {
            var branches = await _receptionService.GetBranchesAsync(CurrentTenantId);
            if (IsAdminOrSuper) return branches;
            var scoped = User.AssignedBranchIds();
            return scoped.Count == 0
                ? branches
                : branches.Where(b => scoped.Contains(b.BranchId)).ToList();
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
                todayEntryCount       = dashboard.TodayEntryCount,
                maxCapacity           = dashboard.MaxCapacity
            });
        }

        // ── GET /Reception/LatestCheckIn  (AJAX polling) ─────────
        /// <summary>
        /// Returns the most recent check-in after sinceUtc. Called every 3 s by the
        /// reception page to detect mobile-app check-ins and show the auto popup.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LatestCheckIn(Guid branchId, DateTime sinceUtc)
        {
            if (branchId == Guid.Empty) return BadRequest();

            var tenantId = CurrentTenantId;
            var result = await _receptionService.GetLatestCheckInAsync(branchId, tenantId, sinceUtc);

            if (result == null) return Ok(new { found = false });

            return Ok(new
            {
                found             = true,
                attendanceRecordId = result.AttendanceRecordId,
                memberId          = result.MemberId,
                memberName        = result.MemberName,
                membershipNumber  = result.MembershipNumber,
                photoUrl          = result.PhotoUrl,
                phoneNumber       = result.PhoneNumber,
                packageName       = result.PackageName,
                sessionsRemaining = result.SessionsRemaining,
                checkInAtUtc      = result.CheckInAtUtc,
                requiresChoice    = result.RequiresChoice,
                packageOptions    = result.PackageOptions.Select(o => new
                {
                    memberPackageId   = o.MemberPackageId,
                    packageName       = o.PackageName,
                    packageTypeCode   = o.PackageTypeCode,
                    sessionsRemaining = o.SessionsRemaining,
                    label             = o.Label,
                }),
            });
        }

        // ── POST /Reception/ConfirmPending  (AJAX) ────────────────
        /// <summary>
        /// Finalizes a PENDING mobile-scan record once the receptionist picks the
        /// class vs open-gym package the member is attending under.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPending([FromBody] ConfirmPendingRequestModel model)
        {
            if (model.AttendanceRecordId == Guid.Empty || model.SelectedMemberPackageId == Guid.Empty)
                return BadRequest(new { success = false, errorMessage = "Invalid request." });

            var tenantId = CurrentTenantId;
            var result = await _receptionService.ConfirmPendingAsync(
                model.AttendanceRecordId, model.SelectedMemberPackageId, CurrentUserId, tenantId);

            return Ok(result);
        }

        // ── GET /Reception/PendingOptions  (AJAX) ─────────────────
        /// <summary>
        /// Returns the class/open-gym options for a single PENDING mobile scan by
        /// its record id — so the confirm popup can be re-opened from the
        /// notifications list or the attendance log, not only the live poll.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PendingOptions(Guid attendanceRecordId)
        {
            if (attendanceRecordId == Guid.Empty)
                return BadRequest(new { found = false });

            var result = await _receptionService.GetPendingOptionsAsync(
                attendanceRecordId, CurrentTenantId);

            if (!result.Found)
                return Ok(new { found = false });

            return Ok(new
            {
                found        = true,
                stillPending = result.StillPending,
                memberName   = result.MemberName,
                branchName   = result.BranchName,
                options      = result.Options.Select(o => new
                {
                    memberPackageId   = o.MemberPackageId,
                    packageName       = o.PackageName,
                    packageTypeCode   = o.PackageTypeCode,
                    sessionsRemaining = o.SessionsRemaining,
                    label             = o.Label,
                }),
            });
        }

        // ── POST /Reception/RecordPtSession  (AJAX) ───────────────
        /// <summary>
        /// Records that a member attended a PT session with a chosen coach.
        /// Deducts one session from a standalone Personal Training plan.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPtSession([FromBody] RecordPtSessionRequestModel model)
        {
            if (model.MemberId == Guid.Empty || model.MemberPackageId == Guid.Empty
                || model.CoachId == Guid.Empty || model.BranchId == Guid.Empty)
                return BadRequest(new { success = false, errorMessage = "Invalid request." });

            var result = await _receptionService.RecordPtSessionAsync(
                model.MemberId, model.MemberPackageId, model.CoachId, model.BranchId,
                CurrentUserId, CurrentTenantId);

            return Ok(result);
        }

        // ── POST /Reception/RecordInBody  (AJAX) ──────────────────
        /// <summary>
        /// Records that a member did an InBody scan. Deducts one from InBodyRemaining.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordInBody([FromBody] RecordInBodyRequestModel model)
        {
            if (model.MemberId == Guid.Empty || model.MemberPackageId == Guid.Empty
                || model.BranchId == Guid.Empty)
                return BadRequest(new { success = false, errorMessage = "Invalid request." });

            var result = await _receptionService.RecordInBodyAsync(
                model.MemberId, model.MemberPackageId, model.BranchId,
                CurrentUserId, CurrentTenantId);

            return Ok(result);
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

    public class ConfirmPendingRequestModel
    {
        public Guid AttendanceRecordId { get; set; }
        public Guid SelectedMemberPackageId { get; set; }
    }

    public class RecordPtSessionRequestModel
    {
        public Guid MemberId { get; set; }
        public Guid MemberPackageId { get; set; }
        public Guid CoachId { get; set; }
        public Guid BranchId { get; set; }
    }

    public class RecordInBodyRequestModel
    {
        public Guid MemberId { get; set; }
        public Guid MemberPackageId { get; set; }
        public Guid BranchId { get; set; }
    }
}
