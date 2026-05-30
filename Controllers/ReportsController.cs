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
    [Authorize(Policy = "AdminAndAbove")]
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
                    label = $"{from:MMM d, yyyy} – {to:MMM d, yyyy}";
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

        private async Task<List<BranchDropdownItem>> GetBranchesAsync() =>
            await _db.Branches
                .Where(b => b.TenantId == TenantId && b.IsActive)
                .OrderBy(b => b.BranchName)
                .Select(b => new BranchDropdownItem
                {
                    BranchId = b.BranchId,
                    BranchName = b.BranchName,
                })
                .ToListAsync();
    }
}
