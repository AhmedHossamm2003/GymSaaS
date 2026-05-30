using GymSaaS.Models;
using GymSaaS.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly GymDbContext _db;

        public AttendanceController(GymDbContext db) => _db = db;

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        // GET /Attendance
        public async Task<IActionResult> Index(
            string?  search,
            Guid?    branchId,
            string?  statusCode,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page = 1)
        {
            const int pageSize = 30;
            var tenantId = TenantId;

            // Default to today
            var today = DateOnly.FromDateTime(DateTime.Today);
            fromDate ??= today;
            toDate   ??= today;

            var fromDt = fromDate.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var toDt   = toDate.Value.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();

            // Base query — EF translates all navigations to JOINs
            var q = _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.CheckInAtUtc >= fromDt
                         && a.CheckInAtUtc <= toDt);

            if (branchId.HasValue)
                q = q.Where(a => a.BranchId == branchId.Value);

            if (!string.IsNullOrWhiteSpace(statusCode))
                q = q.Where(a => a.AttendanceStatus.StatusCode == statusCode);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(a =>
                    a.Member.FirstName.Contains(s) ||
                    a.Member.LastName.Contains(s)  ||
                    a.Member.MembershipNumber.Contains(s));
            }

            // Stats for the filtered period (before pagination)
            var stats = new AttendanceStatsDto
            {
                TotalCheckIns     = await q.CountAsync(),
                SessionsDeducted  = await q.SumAsync(a => (int?)a.SessionsDeductedCount) ?? 0,
                CrossBranchVisits = await q.CountAsync(a => a.IsCrossBranchVisit),
                OverrideCheckIns  = await q.CountAsync(a => a.OverrideApplied),
                UniqueMembers     = await q.Select(a => a.MemberId).Distinct().CountAsync(),
            };

            var totalPages = (int)Math.Ceiling(stats.TotalCheckIns / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var records = await q
                .OrderByDescending(a => a.CheckInAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AttendanceLogItem
                {
                    AttendanceRecordId = a.AttendanceRecordId,
                    MemberId           = a.MemberId,
                    MemberName         = a.Member.FullName ?? (a.Member.FirstName + " " + a.Member.LastName),
                    MembershipNumber   = a.Member.MembershipNumber,
                    MemberPhotoUrl     = a.Member.ProfileImageUrl,
                    BranchName         = a.Branch.BranchName,
                    StatusCode         = a.AttendanceStatus.StatusCode,
                    StatusName         = a.AttendanceStatus.StatusName,
                    CheckInAtUtc       = a.CheckInAtUtc,
                    PresenceUntilUtc   = a.PresenceUntilUtc,
                    SessionDeducted    = a.SessionDeducted,
                    SessionsDeducted   = a.SessionsDeductedCount,
                    IsCrossBranch      = a.IsCrossBranchVisit,
                    OverrideApplied    = a.OverrideApplied,
                    PackageName        = a.MemberPackage != null
                                            ? a.MemberPackage.PackageNameSnapshot
                                            : null,
                    Notes              = a.Notes,
                })
                .ToListAsync();

            // Populate dropdowns
            var branches = await _db.Branches
                .Where(b => b.TenantId == tenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new { b.BranchId, b.BranchName })
                .ToListAsync();

            var statuses = await _db.AttendanceStatuses
                .OrderBy(s => s.StatusName)
                .Select(s => new { s.StatusCode, s.StatusName })
                .ToListAsync();

            ViewData["Title"]      = "Attendance";
            ViewData["Subtitle"]   = "Check-in log";
            ViewData["Search"]     = search;
            ViewData["BranchId"]   = branchId;
            ViewData["StatusCode"] = statusCode;
            ViewData["FromDate"]   = fromDate;
            ViewData["ToDate"]     = toDate;
            ViewData["Page"]       = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["Stats"]      = stats;
            ViewData["Branches"]   = branches.Select(b =>
                                        new BranchDropdownItem { BranchId = b.BranchId, BranchName = b.BranchName })
                                        .ToList();
            ViewData["Statuses"]   = statuses.Select(s => (s.StatusCode, s.StatusName)).ToList();

            return View(records);
        }
    }
}
