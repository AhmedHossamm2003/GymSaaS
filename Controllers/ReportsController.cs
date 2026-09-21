using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Services;
using GymSaaS.Services.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [GymSaaS.Authorization.ViewPermissionAuthorize]
    public class ReportsController : Controller
    {
        private readonly GymDbContext _db;
        private readonly IReportsService _reports;
        private readonly IPdfExportService _pdf;
        private readonly IExcelExportService _excel;

        public ReportsController(
            GymDbContext db,
            IReportsService reports,
            IPdfExportService pdf,
            IExcelExportService excel)
        {
            _db = db;
            _reports = reports;
            _pdf = pdf;
            _excel = excel;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        // ─────────────────────────────────────────────
        // GET /Reports
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index(
            string preset = "this_month",
            DateOnly? fromDate = null, DateOnly? toDate = null,
            Guid? branchId = null)
        {
            var (from, to, label) = ResolveRange(preset, fromDate, toDate);
            branchId = ClampBranch(branchId);

            var vm = await _reports.BuildAsync(TenantId, from, to, branchId, preset);
            vm.RangeLabel = label;

            ViewData["Title"]    = "Reports";
            ViewData["Subtitle"] = "Performance dashboard";
            ViewData["Branches"] = await GetBranchesAsync();

            return View(vm);
        }

        // ─────────────────────────────────────────────
        // GET /Reports/ExportPdf
        // ─────────────────────────────────────────────
        public async Task<IActionResult> ExportPdf(
            string preset = "this_month",
            DateOnly? fromDate = null, DateOnly? toDate = null,
            Guid? branchId = null)
        {
            var (from, to, label) = ResolveRange(preset, fromDate, toDate);
            branchId = ClampBranch(branchId);
            var vm = await _reports.BuildAsync(TenantId, from, to, branchId, preset);
            vm.RangeLabel = label;

            var tenantName = await _db.Tenants
                .Where(t => t.TenantId == TenantId)
                .Select(t => t.TenantName)
                .FirstOrDefaultAsync() ?? "Gym";

            var bytes = _pdf.GenerateReport(vm, tenantName);
            var fileName = $"Report_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        // ─────────────────────────────────────────────
        // GET /Reports/ExportExcel
        // ─────────────────────────────────────────────
        public async Task<IActionResult> ExportExcel(
            string preset = "this_month",
            DateOnly? fromDate = null, DateOnly? toDate = null,
            Guid? branchId = null)
        {
            var (from, to, label) = ResolveRange(preset, fromDate, toDate);
            branchId = ClampBranch(branchId);
            var vm = await _reports.BuildAsync(TenantId, from, to, branchId, preset);
            vm.RangeLabel = label;

            var tenantName = await _db.Tenants
                .Where(t => t.TenantId == TenantId)
                .Select(t => t.TenantName)
                .FirstOrDefaultAsync() ?? "Gym";

            var bytes = _excel.GenerateReport(vm, tenantName);
            var fileName = $"Report_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────
        private static (DateOnly from, DateOnly to, string label) ResolveRange(
            string preset, DateOnly? fromDate, DateOnly? toDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            DateOnly from, to;
            string label;

            switch (preset)
            {
                case "today":
                    from = to = today;
                    label = $"Today · {today:MMM d, yyyy}";
                    break;

                case "yesterday":
                    from = to = today.AddDays(-1);
                    label = $"Yesterday · {from:MMM d, yyyy}";
                    break;

                // A specific day the user picked from the date box.
                case "day":
                    from = to = fromDate ?? today;
                    label = from.ToString("dddd, MMM d, yyyy");
                    break;

                case "this_week":
                    from = StartOfWeek(today);
                    to   = today;
                    label = $"This Week · {from:MMM d} – {from.AddDays(6):MMM d}";
                    break;

                case "last_week":
                    from = StartOfWeek(today).AddDays(-7);
                    to   = from.AddDays(6);
                    label = $"Last Week · {from:MMM d} – {to:MMM d, yyyy}";
                    break;

                case "last_month":
                    var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
                    to   = firstOfThisMonth.AddDays(-1);
                    from = new DateOnly(to.Year, to.Month, 1);
                    label = from.ToString("MMMM yyyy");
                    break;

                case "last_3_months":
                    to   = today;
                    from = today.AddMonths(-3).AddDays(1);
                    label = "Last 3 Months";
                    break;

                case "last_6_months":
                    to   = today;
                    from = today.AddMonths(-6).AddDays(1);
                    label = "Last 6 Months";
                    break;

                case "this_year":
                    from = new DateOnly(today.Year, 1, 1);
                    to   = today;
                    label = today.Year.ToString();
                    break;

                case "last_year":
                    from = new DateOnly(today.Year - 1, 1, 1);
                    to   = new DateOnly(today.Year - 1, 12, 31);
                    label = (today.Year - 1).ToString();
                    break;

                case "custom":
                    from = fromDate ?? new DateOnly(today.Year, today.Month, 1);
                    to   = toDate   ?? today;
                    // Tolerate a backwards range rather than reporting on nothing.
                    if (from > to) (from, to) = (to, from);
                    label = from == to
                        ? from.ToString("dddd, MMM d, yyyy")
                        : $"{from:MMM d, yyyy} – {to:MMM d, yyyy}";
                    break;

                case "this_month":
                default:
                    from = new DateOnly(today.Year, today.Month, 1);
                    to   = today;
                    label = today.ToString("MMMM yyyy");
                    break;
            }

            return (from, to, label);
        }

        // Gyms here run a Saturday-start week, so weekly reports line up with how
        // staff already think about the week. Change this one constant to shift it.
        private const DayOfWeek WeekStartsOn = DayOfWeek.Saturday;

        private static DateOnly StartOfWeek(DateOnly date)
        {
            int delta = ((int)date.DayOfWeek - (int)WeekStartsOn + 7) % 7;
            return date.AddDays(-delta);
        }

        private async Task<List<BranchDropdownItem>> GetBranchesAsync()
        {
            var scoped = User.AssignedBranchIds();
            return await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive
                         && (scoped.Count == 0 || scoped.Contains(b.BranchId)))
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem
                {
                    BranchId = b.BranchId,
                    BranchName = b.BranchName,
                })
                .ToListAsync();
        }

        // For a branch-restricted user, force the report to one of their branches.
        // Unrestricted users keep whatever branch (or all) they requested.
        private Guid? ClampBranch(Guid? branchId)
        {
            var scoped = User.AssignedBranchIds();
            if (scoped.Count == 0) return branchId;
            if (branchId.HasValue && scoped.Contains(branchId.Value)) return branchId;
            return scoped[0];
        }
    }
}
