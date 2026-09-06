using GymSaaS.Persistence;
using GymSaaS.Models;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [GymSaaS.Authorization.ViewPermissionAuthorize]
    public class DashboardController : Controller
    {
        private readonly GymDbContext _db;

        public DashboardController(GymDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            // Pure Coach users get redirected to their own dashboard
            if (User.IsInRole("Coach")
                && !User.IsInRole("SuperAdmin")
                && !User.IsInRole("Admin")
                && !User.IsInRole("BranchManager")
                && !User.IsInRole("Receptionist"))
            {
                return RedirectToAction("Dashboard", "Coaches");
            }

            var tenantId = Guid.Parse(User.FindFirstValue("TenantId")!);
            var now      = DateTime.UtcNow;
            var today    = DateOnly.FromDateTime(now);

            // Branch scope — restricted staff see only their branches' figures.
            var scoped = User.AssignedBranchIds();
            bool unscoped = scoped.Count == 0;

            // ── 1. Active Members ────────────────────────────────
            var activeMembers = await _db.Members
                .CountAsync(m => m.TenantId == tenantId
                              && m.IsActive
                              && !m.IsDeleted
                              && (unscoped || scoped.Contains(m.HomeBranchId)));

            // ── 2. Attendance Today (scoped branches) ────────────
            var attendanceToday = await _db.AttendanceRecords
                .CountAsync(a => a.TenantId == tenantId
                              && a.CheckInAtUtc.Date == now.Date
                              && (unscoped || scoped.Contains(a.BranchId)));

            // ── 3. Members Currently Inside (within presence window)
            var currentlyInside = await _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.PresenceUntilUtc > now
                         && (unscoped || scoped.Contains(a.BranchId)))
                .Select(a => a.MemberId)
                .Distinct()
                .CountAsync();

            // ── 4. Nearly Expiring Packages (next 7 days) ────────
            var expiringPackages = await _db.MemberPackages
                .CountAsync(mp => mp.TenantId == tenantId
                               && mp.Status == "ACTIVE"
                               && mp.ValidToDate != null
                               && mp.ValidToDate >= today
                               && mp.ValidToDate <= today.AddDays(7)
                               && (unscoped || scoped.Contains(mp.HomeBranchId)));

            // ── 5. Branch Summary ────────────────────────────────
            var branches = await _db.Branches
                .Where(b => b.TenantId == tenantId && b.IsActive
                         && (unscoped || scoped.Contains(b.BranchId)))
                .Select(b => new BranchSummaryItem
                {
                    BranchId   = b.BranchId,
                    BranchName = b.BranchName,
                    City       = b.City ?? "—",
                })
                .ToListAsync();

            // Enrich each branch with today's attendance + currently inside
            foreach (var branch in branches)
            {
                branch.AttendanceToday = await _db.AttendanceRecords
                    .CountAsync(a => a.BranchId == branch.BranchId
                                  && a.CheckInAtUtc.Date == now.Date);

                branch.CurrentlyInside = await _db.AttendanceRecords
                    .Where(a => a.BranchId == branch.BranchId
                             && a.PresenceUntilUtc > now)
                    .Select(a => a.MemberId)
                    .Distinct()
                    .CountAsync();
            }

            // ── 6. Recent Attendance (last 10 check-ins) ─────────
            var recentAttendance = await _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && (unscoped || scoped.Contains(a.BranchId)))
                .OrderByDescending(a => a.CheckInAtUtc)
                .Take(10)
                .Join(_db.Members,
                      a => a.MemberId,
                      m => m.MemberId,
                      (a, m) => new RecentAttendanceItem
                      {
                          MemberName       = m.FirstName + " " + m.LastName,
                          MembershipNumber = m.MembershipNumber,
                          ProfileImageUrl  = m.ProfileImageUrl,
                          CheckInAtUtc     = a.CheckInAtUtc,
                          BranchId         = a.BranchId,
                          OverrideApplied  = a.OverrideApplied,
                      })
                .ToListAsync();

            // Attach branch names to recent attendance
            var branchIds   = recentAttendance.Select(r => r.BranchId).Distinct().ToList();
            var branchNames = await _db.Branches
                .Where(b => branchIds.Contains(b.BranchId))
                .ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

            foreach (var item in recentAttendance)
                item.BranchName = branchNames.TryGetValue(item.BranchId, out var name) ? name : "—";

            // ── 7. Low Session Members (≤ 2 sessions remaining) ──
            var lowSessionCount = await _db.MemberPackages
                .CountAsync(mp => mp.TenantId == tenantId
                               && mp.Status == "ACTIVE"
                               && mp.SessionCountRemaining != null
                               && mp.SessionCountRemaining <= 2
                               && (unscoped || scoped.Contains(mp.HomeBranchId)));

            // ── Build ViewModel ───────────────────────────────────
            var vm = new DashboardViewModel
            {
                ActiveMembers      = activeMembers,
                AttendanceToday    = attendanceToday,
                CurrentlyInside    = currentlyInside,
                ExpiringPackages   = expiringPackages,
                LowSessionMembers  = lowSessionCount,
                BranchSummaries    = branches,
                RecentAttendance   = recentAttendance,
            };

            ViewData["Title"] = "Dashboard";
            return View(vm);
        }
    }
}
