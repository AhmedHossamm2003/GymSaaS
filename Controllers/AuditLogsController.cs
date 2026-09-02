using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    // A chronological activity feed assembled from existing data — check-ins,
    // package assignments, PT/InBody perk usage, and user logins. Branch-scoped.
    [Authorize(Policy = "AdminAndAbove")]
    public class AuditLogsController : Controller
    {
        private readonly GymDbContext _db;

        public AuditLogsController(GymDbContext db) => _db = db;

        private Guid TenantId => Guid.Parse(User.FindFirstValue("TenantId")!);

        public async Task<IActionResult> Index(string? category)
        {
            var tenantId = TenantId;
            var scoped   = User.AssignedBranchIds();
            bool unscoped = scoped.Count == 0;
            var since = DateTime.UtcNow.AddDays(-30);

            var items = new List<ActivityLogItem>();

            // ── Check-ins ────────────────────────────────────────────
            if (category is null or "CHECKIN")
            {
                var checkins = await _db.AttendanceRecords
                    .Where(a => a.TenantId == tenantId
                             && a.CheckInAtUtc >= since
                             && (unscoped || scoped.Contains(a.BranchId)))
                    .OrderByDescending(a => a.CheckInAtUtc)
                    .Take(200)
                    .Select(a => new
                    {
                        a.MemberId,
                        a.CheckInAtUtc,
                        MemberName = a.Member.FirstName + " " + a.Member.LastName,
                        BranchName = a.Branch.BranchName,
                        Status = a.AttendanceStatus.StatusCode,
                    })
                    .ToListAsync();

                items.AddRange(checkins.Select(c => new ActivityLogItem
                {
                    WhenUtc = c.CheckInAtUtc,
                    Category = "CHECKIN",
                    Icon = "bi-door-open",
                    Color = "#16a34a",
                    Actor = c.MemberName.Trim(),
                    Action = c.Status == "PENDING" ? "scanned in (pending choice)" : "checked in",
                    Detail = c.BranchName,
                    MemberId = c.MemberId,
                }));
            }

            // ── Package assignments ──────────────────────────────────
            if (category is null or "PACKAGE")
            {
                var packages = await _db.MemberPackages
                    .Where(p => p.TenantId == tenantId
                             && p.CreatedAtUtc >= since
                             && (unscoped || scoped.Contains(p.HomeBranchId)))
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Take(200)
                    .Select(p => new
                    {
                        p.MemberId,
                        p.CreatedAtUtc,
                        p.PackageNameSnapshot,
                        MemberName = p.Member.FirstName + " " + p.Member.LastName,
                        ByUser = p.CreatedByUser != null ? p.CreatedByUser.FirstName + " " + p.CreatedByUser.LastName : null,
                    })
                    .ToListAsync();

                items.AddRange(packages.Select(p => new ActivityLogItem
                {
                    WhenUtc = p.CreatedAtUtc,
                    Category = "PACKAGE",
                    Icon = "bi-box-seam",
                    Color = "#ff5b14",
                    Actor = (p.ByUser ?? "System").Trim(),
                    Action = $"assigned \"{p.PackageNameSnapshot}\" to {p.MemberName.Trim()}",
                    Detail = null,
                    MemberId = p.MemberId,
                }));
            }

            // ── PT / InBody perk usage ───────────────────────────────
            if (category is null or "PERK")
            {
                var perks = await _db.MemberPerkUsages
                    .Where(u => u.TenantId == tenantId
                             && u.UsedAtUtc >= since
                             && (unscoped || scoped.Contains(u.BranchId)))
                    .OrderByDescending(u => u.UsedAtUtc)
                    .Take(200)
                    .ToListAsync();

                // Resolve member + coach names in memory.
                var memberIds = perks.Select(p => p.MemberId).Distinct().ToList();
                var memberNames = await _db.Members
                    .Where(m => memberIds.Contains(m.MemberId))
                    .Select(m => new { m.MemberId, Name = m.FirstName + " " + m.LastName })
                    .ToDictionaryAsync(x => x.MemberId, x => x.Name.Trim());

                var coachIds = perks.Where(p => p.CoachId.HasValue).Select(p => p.CoachId!.Value).Distinct().ToList();
                var coachNames = coachIds.Count > 0
                    ? await _db.Coaches.Where(c => coachIds.Contains(c.CoachId))
                        .Select(c => new { c.CoachId, Name = c.FirstName + " " + c.LastName })
                        .ToDictionaryAsync(x => x.CoachId, x => x.Name.Trim())
                    : new Dictionary<Guid, string>();

                items.AddRange(perks.Select(u => new ActivityLogItem
                {
                    WhenUtc = u.UsedAtUtc,
                    Category = "PERK",
                    Icon = u.PerkType == "PT" ? "bi-person-arms-up" : "bi-clipboard2-pulse",
                    Color = u.PerkType == "PT" ? "#7c3aed" : "#0891b2",
                    Actor = memberNames.GetValueOrDefault(u.MemberId, "Member"),
                    Action = u.PerkType == "PT"
                        ? $"attended a PT session{(u.CoachId.HasValue && coachNames.TryGetValue(u.CoachId.Value, out var cn) ? $" with {cn}" : "")}"
                        : "completed an InBody scan",
                    Detail = null,
                    MemberId = u.MemberId,
                }));
            }

            // ── User logins (admins only / unscoped) ─────────────────
            if ((category is null or "LOGIN") && unscoped)
            {
                var logins = await _db.Users
                    .Where(u => u.TenantId == tenantId
                             && u.LastLoginAtUtc != null
                             && u.LastLoginAtUtc >= since)
                    .OrderByDescending(u => u.LastLoginAtUtc)
                    .Take(100)
                    .Select(u => new
                    {
                        u.LastLoginAtUtc,
                        Name = u.FirstName + " " + u.LastName,
                    })
                    .ToListAsync();

                items.AddRange(logins.Select(l => new ActivityLogItem
                {
                    WhenUtc = l.LastLoginAtUtc!.Value,
                    Category = "LOGIN",
                    Icon = "bi-box-arrow-in-right",
                    Color = "#3538cd",
                    Actor = l.Name.Trim(),
                    Action = "signed in",
                    Detail = null,
                }));
            }

            var vm = new AuditLogsViewModel
            {
                CategoryFilter = category,
                Items = items.OrderByDescending(i => i.WhenUtc).Take(300).ToList(),
            };

            ViewData["Title"] = "Audit Logs";
            ViewData["Subtitle"] = "Recent activity (last 30 days)";
            return View(vm);
        }
    }
}
