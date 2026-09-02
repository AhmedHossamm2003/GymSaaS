using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    // Notifications are derived live from current data (no dedicated table):
    // expiring/expired packages, low session counts, and mobile scans awaiting
    // a reception decision. All branch-scoped to the signed-in user.
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly GymDbContext _db;

        public NotificationsController(GymDbContext db) => _db = db;

        private Guid TenantId => Guid.Parse(User.FindFirstValue("TenantId")!);

        public async Task<IActionResult> Index()
        {
            var vm = await BuildAsync();
            ViewData["Title"] = "Notifications";
            ViewData["Subtitle"] = "Alerts that need attention";
            return View(vm);
        }

        // Lightweight count for the topbar bell dot.
        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var vm = await BuildAsync();
            return Json(new { count = vm.TotalCount });
        }

        private async Task<NotificationsViewModel> BuildAsync()
        {
            var tenantId = TenantId;
            var scoped   = User.AssignedBranchIds();
            bool unscoped = scoped.Count == 0;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var vm = new NotificationsViewModel();

            // ── Expiring packages (next 7 days) ──────────────────────
            var expiring = await _db.MemberPackages
                .Where(p => p.TenantId == tenantId
                         && p.Status == "ACTIVE"
                         && p.ValidToDate != null
                         && p.ValidToDate >= today
                         && p.ValidToDate <= today.AddDays(7)
                         && (unscoped || scoped.Contains(p.HomeBranchId)))
                .Select(p => new
                {
                    p.MemberId,
                    p.PackageNameSnapshot,
                    p.ValidToDate,
                    MemberName = p.Member.FirstName + " " + p.Member.LastName,
                })
                .OrderBy(p => p.ValidToDate)
                .Take(100)
                .ToListAsync();

            foreach (var e in expiring)
            {
                vm.Items.Add(new NotificationItem
                {
                    Category = "EXPIRING",
                    Icon = "bi-hourglass-split",
                    Severity = "warning",
                    Title = $"{e.MemberName} — package expiring",
                    Detail = $"{e.PackageNameSnapshot} expires {e.ValidToDate:MMM d, yyyy}",
                    MemberId = e.MemberId,
                    WhenUtc = DateTime.UtcNow,
                });
            }
            vm.ExpiringCount = expiring.Count;

            // ── Recently expired packages (last 7 days) ──────────────
            var expired = await _db.MemberPackages
                .Where(p => p.TenantId == tenantId
                         && p.Status == "ACTIVE"
                         && p.ValidToDate != null
                         && p.ValidToDate < today
                         && p.ValidToDate >= today.AddDays(-7)
                         && (unscoped || scoped.Contains(p.HomeBranchId)))
                .Select(p => new
                {
                    p.MemberId,
                    p.PackageNameSnapshot,
                    p.ValidToDate,
                    MemberName = p.Member.FirstName + " " + p.Member.LastName,
                })
                .OrderByDescending(p => p.ValidToDate)
                .Take(100)
                .ToListAsync();

            foreach (var e in expired)
            {
                vm.Items.Add(new NotificationItem
                {
                    Category = "EXPIRED",
                    Icon = "bi-x-octagon",
                    Severity = "danger",
                    Title = $"{e.MemberName} — package expired",
                    Detail = $"{e.PackageNameSnapshot} expired {e.ValidToDate:MMM d, yyyy}",
                    MemberId = e.MemberId,
                    WhenUtc = DateTime.UtcNow,
                });
            }
            vm.ExpiredCount = expired.Count;

            // ── Low sessions (≤ 2 remaining) ─────────────────────────
            var lowSessions = await _db.MemberPackages
                .Where(p => p.TenantId == tenantId
                         && p.Status == "ACTIVE"
                         && p.SessionCountRemaining != null
                         && p.SessionCountRemaining > 0
                         && p.SessionCountRemaining <= 2
                         && (unscoped || scoped.Contains(p.HomeBranchId)))
                .Select(p => new
                {
                    p.MemberId,
                    p.PackageNameSnapshot,
                    p.SessionCountRemaining,
                    MemberName = p.Member.FirstName + " " + p.Member.LastName,
                })
                .OrderBy(p => p.SessionCountRemaining)
                .Take(100)
                .ToListAsync();

            foreach (var l in lowSessions)
            {
                vm.Items.Add(new NotificationItem
                {
                    Category = "LOW_SESSIONS",
                    Icon = "bi-battery-low",
                    Severity = "warning",
                    Title = $"{l.MemberName} — running low on sessions",
                    Detail = $"{l.SessionCountRemaining} left on {l.PackageNameSnapshot}",
                    MemberId = l.MemberId,
                    WhenUtc = DateTime.UtcNow,
                });
            }
            vm.LowSessionCount = lowSessions.Count;

            // ── Mobile scans awaiting reception choice (PENDING) ─────
            var pendingStatusId = await _db.AttendanceStatuses
                .Where(s => s.StatusCode == "PENDING")
                .Select(s => s.AttendanceStatusId)
                .FirstOrDefaultAsync();

            if (pendingStatusId != Guid.Empty)
            {
                var pending = await _db.AttendanceRecords
                    .Where(a => a.TenantId == tenantId
                             && a.AttendanceStatusId == pendingStatusId
                             && a.CheckInAtUtc >= DateTime.UtcNow.AddHours(-12)
                             && (unscoped || scoped.Contains(a.BranchId)))
                    .OrderByDescending(a => a.CheckInAtUtc)
                    .Select(a => new
                    {
                        a.MemberId,
                        a.CheckInAtUtc,
                        MemberName = a.Member.FirstName + " " + a.Member.LastName,
                        BranchName = a.Branch.BranchName,
                    })
                    .Take(50)
                    .ToListAsync();

                foreach (var p in pending)
                {
                    vm.Items.Add(new NotificationItem
                    {
                        Category = "PENDING_SCAN",
                        Icon = "bi-qr-code-scan",
                        Severity = "info",
                        Title = $"{p.MemberName} — awaiting check-in choice",
                        Detail = $"Scanned at {p.BranchName}, needs class/open-gym confirmation",
                        MemberId = p.MemberId,
                        WhenUtc = p.CheckInAtUtc,
                    });
                }
                vm.PendingScanCount = pending.Count;
            }

            // Most urgent first: expired, then pending, then expiring, then low.
            vm.Items = vm.Items
                .OrderBy(i => i.Severity == "danger" ? 0 : i.Severity == "warning" ? 1 : 2)
                .ThenByDescending(i => i.WhenUtc)
                .ToList();

            return vm;
        }
    }
}
