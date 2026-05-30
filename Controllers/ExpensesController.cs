using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "AdminAndAbove")]
    public class ExpensesController : Controller
    {
        private readonly GymDbContext _db;

        public ExpensesController(GymDbContext db) => _db = db;

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─────────────────────────────────────────────
        // GET /Expenses
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index(
            string? category, Guid? branchId,
            DateOnly? fromDate, DateOnly? toDate,
            int page = 1)
        {
            const int pageSize = 25;
            var tenantId = TenantId;

            // Default to current month if no date range supplied
            var today = DateOnly.FromDateTime(DateTime.Today);
            fromDate ??= new DateOnly(today.Year, today.Month, 1);
            toDate   ??= today;

            var q = _db.Expenses
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                         && e.ExpenseDate >= fromDate.Value
                         && e.ExpenseDate <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(e => e.CategoryCode == category);

            if (branchId.HasValue)
                q = q.Where(e => e.BranchId == branchId.Value);

            var total = await q.CountAsync();
            var sum   = await q.SumAsync(e => (decimal?)e.Amount) ?? 0m;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var items = await q
                .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new ExpenseListItem
                {
                    ExpenseId      = e.ExpenseId,
                    CategoryCode   = e.CategoryCode,
                    VendorName     = e.VendorName,
                    Amount         = e.Amount,
                    ExpenseDate    = e.ExpenseDate,
                    BranchId       = e.BranchId,
                    BranchName     = e.Branch != null ? e.Branch.BranchName : null,
                    PaymentMethod  = e.PaymentMethod,
                    Notes          = e.Notes,
                    CreatedAtUtc   = e.CreatedAtUtc,
                })
                .ToListAsync();

            ViewData["Title"]      = "Expenses";
            ViewData["Subtitle"]   = "Track operating costs";
            ViewData["Category"]   = category;
            ViewData["BranchId"]   = branchId;
            ViewData["FromDate"]   = fromDate;
            ViewData["ToDate"]     = toDate;
            ViewData["Page"]       = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalCount"] = total;
            ViewData["TotalAmount"]= sum;
            ViewData["Branches"]   = await GetBranchesAsync();

            return View(items);
        }

        // ─────────────────────────────────────────────
        // GET /Expenses/Create
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Create()
        {
            var vm = new ExpenseFormViewModel
            {
                Branches = await GetBranchesAsync(),
            };
            ViewData["Title"]    = "Expenses";
            ViewData["Subtitle"] = "Add Expense";
            return View("CreateEdit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExpenseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"]    = "Expenses";
                ViewData["Subtitle"] = "Add Expense";
                return View("CreateEdit", model);
            }

            var expense = new Expense
            {
                ExpenseId       = Guid.NewGuid(),
                TenantId        = TenantId,
                BranchId        = model.BranchId,
                CategoryCode    = model.CategoryCode,
                VendorName      = model.VendorName?.Trim(),
                Amount          = model.Amount,
                ExpenseDate     = model.ExpenseDate,
                PaymentMethod   = model.PaymentMethod,
                Notes           = model.Notes?.Trim(),
                CreatedAtUtc    = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            TempData["Toast"]     = $"Expense of {model.Amount:N2} recorded.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // GET /Expenses/Edit/id
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Edit(Guid id)
        {
            var e = await _db.Expenses
                .FirstOrDefaultAsync(x => x.ExpenseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            var vm = new ExpenseFormViewModel
            {
                ExpenseId     = e.ExpenseId,
                CategoryCode  = e.CategoryCode,
                BranchId      = e.BranchId,
                VendorName    = e.VendorName,
                Amount        = e.Amount,
                ExpenseDate   = e.ExpenseDate,
                PaymentMethod = e.PaymentMethod,
                Notes         = e.Notes,
                Branches      = await GetBranchesAsync(),
            };

            ViewData["Title"]    = "Expenses";
            ViewData["Subtitle"] = "Edit Expense";
            return View("CreateEdit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ExpenseFormViewModel model)
        {
            var e = await _db.Expenses
                .FirstOrDefaultAsync(x => x.ExpenseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"]    = "Expenses";
                ViewData["Subtitle"] = "Edit Expense";
                return View("CreateEdit", model);
            }

            e.CategoryCode    = model.CategoryCode;
            e.BranchId        = model.BranchId;
            e.VendorName      = model.VendorName?.Trim();
            e.Amount          = model.Amount;
            e.ExpenseDate     = model.ExpenseDate;
            e.PaymentMethod   = model.PaymentMethod;
            e.Notes           = model.Notes?.Trim();
            e.UpdatedAtUtc    = DateTime.UtcNow;
            e.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"]     = "Expense updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // POST /Expenses/Delete/id
        // ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var e = await _db.Expenses
                .FirstOrDefaultAsync(x => x.ExpenseId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (e == null) return NotFound();

            e.IsDeleted       = true;
            e.UpdatedAtUtc    = DateTime.UtcNow;
            e.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();

            TempData["Toast"]     = "Expense removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────
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
