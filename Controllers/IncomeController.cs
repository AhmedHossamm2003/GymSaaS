using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    // Manual income entries (day passes, merchandise, etc.)
    // Package sales are tracked automatically via MemberPackage.PackageDefinition.Price.
    [Authorize(Policy = "AdminAndAbove")]
    public class IncomeController : Controller
    {
        private readonly GymDbContext _db;

        public IncomeController(GymDbContext db) => _db = db;

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> Index(
            string? category, Guid? branchId,
            DateOnly? fromDate, DateOnly? toDate,
            int page = 1)
        {
            const int pageSize = 25;
            var tenantId = TenantId;

            var today = DateOnly.FromDateTime(DateTime.Today);
            fromDate ??= new DateOnly(today.Year, today.Month, 1);
            toDate   ??= today;

            var q = _db.ManualIncomeEntries
                .Where(i => i.TenantId == tenantId && !i.IsDeleted
                         && i.IncomeDate >= fromDate.Value
                         && i.IncomeDate <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(i => i.CategoryCode == category);

            if (branchId.HasValue)
                q = q.Where(i => i.BranchId == branchId.Value);

            var total = await q.CountAsync();
            var sum   = await q.SumAsync(i => (decimal?)i.Amount) ?? 0m;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var items = await q
                .OrderByDescending(i => i.IncomeDate).ThenByDescending(i => i.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new ManualIncomeListItem
                {
                    IncomeEntryId  = i.IncomeEntryId,
                    CategoryCode   = i.CategoryCode,
                    Description    = i.Description,
                    Amount         = i.Amount,
                    IncomeDate     = i.IncomeDate,
                    BranchId       = i.BranchId,
                    BranchName     = i.Branch != null ? i.Branch.BranchName : null,
                    PaymentMethod  = i.PaymentMethod,
                    Notes          = i.Notes,
                    CreatedAtUtc   = i.CreatedAtUtc,
                })
                .ToListAsync();

            ViewData["Title"]      = "Income";
            ViewData["Subtitle"]   = "Manual income entries";
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

        public async Task<IActionResult> Create()
        {
            var vm = new ManualIncomeFormViewModel
            {
                Branches = await GetBranchesAsync(),
            };
            ViewData["Title"]    = "Income";
            ViewData["Subtitle"] = "Add Income Entry";
            return View("CreateEdit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ManualIncomeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"]    = "Income";
                ViewData["Subtitle"] = "Add Income Entry";
                return View("CreateEdit", model);
            }

            var entry = new ManualIncomeEntry
            {
                IncomeEntryId   = Guid.NewGuid(),
                TenantId        = TenantId,
                BranchId        = model.BranchId,
                CategoryCode    = model.CategoryCode,
                Description     = model.Description.Trim(),
                Amount          = model.Amount,
                IncomeDate      = model.IncomeDate,
                PaymentMethod   = model.PaymentMethod,
                Notes           = model.Notes?.Trim(),
                CreatedAtUtc    = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.ManualIncomeEntries.Add(entry);
            await _db.SaveChangesAsync();

            TempData["Toast"]     = $"Income of {model.Amount:N2} recorded.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var i = await _db.ManualIncomeEntries
                .FirstOrDefaultAsync(x => x.IncomeEntryId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (i == null) return NotFound();

            var vm = new ManualIncomeFormViewModel
            {
                IncomeEntryId = i.IncomeEntryId,
                CategoryCode  = i.CategoryCode,
                BranchId      = i.BranchId,
                Description   = i.Description,
                Amount        = i.Amount,
                IncomeDate    = i.IncomeDate,
                PaymentMethod = i.PaymentMethod,
                Notes         = i.Notes,
                Branches      = await GetBranchesAsync(),
            };

            ViewData["Title"]    = "Income";
            ViewData["Subtitle"] = "Edit Income Entry";
            return View("CreateEdit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ManualIncomeFormViewModel model)
        {
            var i = await _db.ManualIncomeEntries
                .FirstOrDefaultAsync(x => x.IncomeEntryId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (i == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                ViewData["Title"]    = "Income";
                ViewData["Subtitle"] = "Edit Income Entry";
                return View("CreateEdit", model);
            }

            i.CategoryCode    = model.CategoryCode;
            i.BranchId        = model.BranchId;
            i.Description     = model.Description.Trim();
            i.Amount          = model.Amount;
            i.IncomeDate      = model.IncomeDate;
            i.PaymentMethod   = model.PaymentMethod;
            i.Notes           = model.Notes?.Trim();
            i.UpdatedAtUtc    = DateTime.UtcNow;
            i.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"]     = "Income entry updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var i = await _db.ManualIncomeEntries
                .FirstOrDefaultAsync(x => x.IncomeEntryId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (i == null) return NotFound();

            i.IsDeleted       = true;
            i.UpdatedAtUtc    = DateTime.UtcNow;
            i.UpdatedByUserId = UserId;
            await _db.SaveChangesAsync();

            TempData["Toast"]     = "Income entry removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
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
