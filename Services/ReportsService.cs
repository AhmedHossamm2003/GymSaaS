using GymSaaS.Models;
using GymSaaS.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Services
{
    public interface IReportsService
    {
        Task<ReportsViewModel> BuildAsync(
            Guid tenantId,
            DateOnly fromDate, DateOnly toDate,
            Guid? branchFilterId,
            string rangePreset);
    }

    public class ReportsService : IReportsService
    {
        private readonly GymDbContext _db;

        public ReportsService(GymDbContext db) => _db = db;

        public async Task<ReportsViewModel> BuildAsync(
            Guid tenantId,
            DateOnly fromDate, DateOnly toDate,
            Guid? branchFilterId,
            string rangePreset)
        {
            var vm = new ReportsViewModel
            {
                RangeStart     = fromDate,
                RangeEnd       = toDate,
                RangePreset    = rangePreset,
                RangeLabel     = $"{fromDate:MMM d, yyyy} – {toDate:MMM d, yyyy}",
                BranchFilterId = branchFilterId,
            };

            // Branch filter name
            if (branchFilterId.HasValue)
            {
                vm.BranchFilterName = await _db.Branches
                    .Where(b => b.BranchId == branchFilterId.Value)
                    .Select(b => b.BranchName)
                    .FirstOrDefaultAsync();
            }

            // ── Period length & comparison range ───────────────────────
            int periodDays = toDate.DayNumber - fromDate.DayNumber + 1;
            vm.ComparisonStart = fromDate.AddDays(-periodDays);
            vm.ComparisonEnd   = fromDate.AddDays(-1);

            var fromDt = fromDate.ToDateTime(TimeOnly.MinValue);
            var toDt   = toDate.ToDateTime(TimeOnly.MaxValue);
            var prevFromDt = vm.ComparisonStart.ToDateTime(TimeOnly.MinValue);
            var prevToDt   = vm.ComparisonEnd.ToDateTime(TimeOnly.MaxValue);

            // ─── INCOME ────────────────────────────────────────────────
            // Auto income from package assignments (PackageDefinition.Price × count)
            var packageIncomeQuery = _db.MemberPackages
                .Where(mp => mp.TenantId == tenantId
                          && mp.Status == "ACTIVE"
                          && mp.CreatedAtUtc >= fromDt
                          && mp.CreatedAtUtc <= toDt)
                .Join(_db.PackageDefinitions, mp => mp.PackageDefinitionId, pd => pd.PackageDefinitionId, (mp, pd) => new { mp, pd });

            if (branchFilterId.HasValue)
                packageIncomeQuery = packageIncomeQuery.Where(x => x.mp.HomeBranchId == branchFilterId.Value);

            // Avoid double-counting COMBINED rows (they have two MemberPackage rows but one sale)
            // → group by LinkedPackageGroupId when present; otherwise count individually
            var pkgRows = await packageIncomeQuery
                .Select(x => new
                {
                    GroupKey         = x.mp.LinkedPackageGroupId ?? x.mp.MemberPackageId,
                    Price            = x.pd.Price ?? 0,
                    PackageName      = x.pd.PackageName,
                    HomeBranchId     = x.mp.HomeBranchId,
                    ComponentRole    = x.mp.PackageComponentRole,
                    LinkedGroupId    = x.mp.LinkedPackageGroupId,
                })
                .ToListAsync();

            var pkgGroupedForIncome = pkgRows
                .GroupBy(r => r.GroupKey)
                .Select(g => new
                {
                    Price        = g.First().Price,
                    PackageName  = g.First().PackageName,
                    HomeBranchId = g.First().HomeBranchId,
                })
                .ToList();

            vm.IncomeFromPackages = pkgGroupedForIncome.Sum(g => g.Price);

            // Manual income
            var manualIncQuery = _db.ManualIncomeEntries
                .Where(i => i.TenantId == tenantId && !i.IsDeleted
                         && i.IncomeDate >= fromDate && i.IncomeDate <= toDate);

            if (branchFilterId.HasValue)
                manualIncQuery = manualIncQuery.Where(i => i.BranchId == branchFilterId.Value);

            vm.IncomeFromManual = await manualIncQuery.SumAsync(i => (decimal?)i.Amount) ?? 0m;

            // ─── EXPENSES ──────────────────────────────────────────────
            var expQuery = _db.Expenses
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                         && e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate);

            if (branchFilterId.HasValue)
                expQuery = expQuery.Where(e => e.BranchId == branchFilterId.Value);

            vm.TotalExpenses = await expQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;

            // ─── PREVIOUS PERIOD comparison ────────────────────────────
            var prevPkgQuery = _db.MemberPackages
                .Where(mp => mp.TenantId == tenantId
                          && mp.Status == "ACTIVE"
                          && mp.CreatedAtUtc >= prevFromDt
                          && mp.CreatedAtUtc <= prevToDt)
                .Join(_db.PackageDefinitions, mp => mp.PackageDefinitionId, pd => pd.PackageDefinitionId, (mp, pd) => new { mp, pd });

            if (branchFilterId.HasValue)
                prevPkgQuery = prevPkgQuery.Where(x => x.mp.HomeBranchId == branchFilterId.Value);

            var prevPkgRows = await prevPkgQuery
                .Select(x => new
                {
                    GroupKey = x.mp.LinkedPackageGroupId ?? x.mp.MemberPackageId,
                    Price    = x.pd.Price ?? 0,
                }).ToListAsync();

            var prevPkgIncome = prevPkgRows.GroupBy(r => r.GroupKey).Sum(g => g.First().Price);

            var prevManualQuery = _db.ManualIncomeEntries
                .Where(i => i.TenantId == tenantId && !i.IsDeleted
                         && i.IncomeDate >= vm.ComparisonStart && i.IncomeDate <= vm.ComparisonEnd);

            if (branchFilterId.HasValue)
                prevManualQuery = prevManualQuery.Where(i => i.BranchId == branchFilterId.Value);

            var prevManualIncome = await prevManualQuery.SumAsync(i => (decimal?)i.Amount) ?? 0m;
            vm.PrevIncome = prevPkgIncome + prevManualIncome;

            var prevExpQuery = _db.Expenses
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                         && e.ExpenseDate >= vm.ComparisonStart && e.ExpenseDate <= vm.ComparisonEnd);

            if (branchFilterId.HasValue)
                prevExpQuery = prevExpQuery.Where(e => e.BranchId == branchFilterId.Value);

            vm.PrevExpenses = await prevExpQuery.SumAsync(e => (decimal?)e.Amount) ?? 0m;

            // ─── MEMBERS ───────────────────────────────────────────────
            var memberQuery = _db.Members.Where(m => m.TenantId == tenantId && !m.IsDeleted);
            if (branchFilterId.HasValue)
                memberQuery = memberQuery.Where(m => m.HomeBranchId == branchFilterId.Value);

            vm.TotalMembers = await memberQuery.CountAsync();

            vm.NewMembersInPeriod = await memberQuery
                .CountAsync(m => m.CreatedAtUtc >= fromDt && m.CreatedAtUtc <= toDt);

            vm.PrevPeriodNewMembers = await memberQuery
                .CountAsync(m => m.CreatedAtUtc >= prevFromDt && m.CreatedAtUtc <= prevToDt);

            // Active members (current state, regardless of period)
            var activeStatusId = await _db.MemberStatuses
                .Where(s => s.StatusCode == "ACTIVE")
                .Select(s => s.MemberStatusId)
                .FirstOrDefaultAsync();

            vm.ActiveMembers = await memberQuery
                .CountAsync(m => m.MemberStatusId == activeStatusId);

            // Churn (rough): members updated in period to non-active
            vm.ChurnedMembers = await memberQuery
                .CountAsync(m => m.UpdatedAtUtc >= fromDt && m.UpdatedAtUtc <= toDt
                              && m.MemberStatusId != activeStatusId);

            // ─── ATTENDANCE ────────────────────────────────────────────
            var attQuery = _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.CheckInAtUtc >= fromDt && a.CheckInAtUtc <= toDt);

            if (branchFilterId.HasValue)
                attQuery = attQuery.Where(a => a.BranchId == branchFilterId.Value);

            vm.TotalCheckInsInPeriod = await attQuery.CountAsync();

            var prevAttQuery = _db.AttendanceRecords
                .Where(a => a.TenantId == tenantId
                         && a.CheckInAtUtc >= prevFromDt && a.CheckInAtUtc <= prevToDt);

            if (branchFilterId.HasValue)
                prevAttQuery = prevAttQuery.Where(a => a.BranchId == branchFilterId.Value);

            vm.PrevPeriodCheckIns = await prevAttQuery.CountAsync();

            // ─── EXPENSE BY CATEGORY ───────────────────────────────────
            var byCat = await expQuery
                .GroupBy(e => e.CategoryCode)
                .Select(g => new
                {
                    Code   = g.Key,
                    Amount = g.Sum(x => x.Amount),
                    Count  = g.Count(),
                })
                .ToListAsync();

            var expTotal = byCat.Sum(c => c.Amount);
            vm.ExpenseByCategory = byCat
                .OrderByDescending(c => c.Amount)
                .Select(c => new CategoryBreakdownItem
                {
                    CategoryCode = c.Code,
                    CategoryName = ExpenseCategories.DisplayName(c.Code),
                    Amount       = c.Amount,
                    Count        = c.Count,
                    Percent      = expTotal > 0 ? Math.Round((c.Amount / expTotal) * 100, 1) : 0,
                })
                .ToList();

            // ─── INCOME BY CATEGORY (manual + auto-grouped) ────────────
            var incomeByCat = new List<CategoryBreakdownItem>();

            if (vm.IncomeFromPackages > 0)
            {
                incomeByCat.Add(new CategoryBreakdownItem
                {
                    CategoryCode = "PACKAGES",
                    CategoryName = "Package Sales",
                    Amount       = vm.IncomeFromPackages,
                    Count        = pkgGroupedForIncome.Count,
                });
            }

            var manualByCat = await manualIncQuery
                .GroupBy(i => i.CategoryCode)
                .Select(g => new
                {
                    Code   = g.Key,
                    Amount = g.Sum(x => x.Amount),
                    Count  = g.Count(),
                })
                .ToListAsync();

            foreach (var c in manualByCat)
            {
                incomeByCat.Add(new CategoryBreakdownItem
                {
                    CategoryCode = c.Code,
                    CategoryName = IncomeCategories.DisplayName(c.Code),
                    Amount       = c.Amount,
                    Count        = c.Count,
                });
            }

            var incTotalForPct = incomeByCat.Sum(c => c.Amount);
            foreach (var c in incomeByCat)
                c.Percent = incTotalForPct > 0 ? Math.Round((c.Amount / incTotalForPct) * 100, 1) : 0;
            vm.IncomeByCategory = incomeByCat.OrderByDescending(c => c.Amount).ToList();

            // ─── BRANCH PERFORMANCE ────────────────────────────────────
            var branches = await _db.Branches
                .Where(b => b.TenantId == tenantId)
                .Select(b => new { b.BranchId, b.BranchName })
                .ToListAsync();

            var branchPerf = new List<BranchPerformanceItem>();
            foreach (var b in branches)
            {
                var bPkgIncome = pkgRows
                    .Where(r => r.HomeBranchId == b.BranchId)
                    .GroupBy(r => r.GroupKey)
                    .Sum(g => g.First().Price);

                var bManualIncome = await _db.ManualIncomeEntries
                    .Where(i => i.TenantId == tenantId && !i.IsDeleted
                             && i.IncomeDate >= fromDate && i.IncomeDate <= toDate
                             && i.BranchId == b.BranchId)
                    .SumAsync(i => (decimal?)i.Amount) ?? 0m;

                var bExpenses = await _db.Expenses
                    .Where(e => e.TenantId == tenantId && !e.IsDeleted
                             && e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate
                             && e.BranchId == b.BranchId)
                    .SumAsync(e => (decimal?)e.Amount) ?? 0m;

                var bNewMembers = await _db.Members
                    .CountAsync(m => m.TenantId == tenantId && !m.IsDeleted
                                  && m.HomeBranchId == b.BranchId
                                  && m.CreatedAtUtc >= fromDt && m.CreatedAtUtc <= toDt);

                var bCheckIns = await _db.AttendanceRecords
                    .CountAsync(a => a.TenantId == tenantId
                                  && a.BranchId == b.BranchId
                                  && a.CheckInAtUtc >= fromDt && a.CheckInAtUtc <= toDt);

                if (bPkgIncome > 0 || bManualIncome > 0 || bExpenses > 0 || bNewMembers > 0 || bCheckIns > 0)
                {
                    branchPerf.Add(new BranchPerformanceItem
                    {
                        BranchId    = b.BranchId,
                        BranchName  = b.BranchName,
                        Income      = bPkgIncome + bManualIncome,
                        Expenses    = bExpenses,
                        NewMembers  = bNewMembers,
                        CheckIns    = bCheckIns,
                    });
                }
            }
            vm.BranchPerformance = branchPerf.OrderByDescending(b => b.Income).ToList();

            // ─── TOP SELLING PACKAGES ──────────────────────────────────
            vm.TopSellingPackages = pkgGroupedForIncome
                .GroupBy(g => g.PackageName)
                .Select(g => new TopPackageItem
                {
                    PackageName     = g.Key,
                    AssignmentCount = g.Count(),
                    TotalRevenue    = g.Sum(x => x.Price),
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(5)
                .ToList();

            // ─── MONTHLY TREND (last 6 months ending at RangeEnd) ──────
            var monthlyTrend = new List<MonthlyDataPoint>();
            var trendMonthStart = new DateOnly(toDate.Year, toDate.Month, 1).AddMonths(-5);

            for (int i = 0; i < 6; i++)
            {
                var mStart = trendMonthStart.AddMonths(i);
                var mEnd   = mStart.AddMonths(1).AddDays(-1);

                var mStartDt = mStart.ToDateTime(TimeOnly.MinValue);
                var mEndDt   = mEnd.ToDateTime(TimeOnly.MaxValue);

                // Package income for month
                var mPkgQuery = _db.MemberPackages
                    .Where(mp => mp.TenantId == tenantId && mp.Status == "ACTIVE"
                              && mp.CreatedAtUtc >= mStartDt && mp.CreatedAtUtc <= mEndDt)
                    .Join(_db.PackageDefinitions, mp => mp.PackageDefinitionId, pd => pd.PackageDefinitionId, (mp, pd) => new { mp, pd });

                if (branchFilterId.HasValue)
                    mPkgQuery = mPkgQuery.Where(x => x.mp.HomeBranchId == branchFilterId.Value);

                var mPkgRows = await mPkgQuery
                    .Select(x => new
                    {
                        GroupKey = x.mp.LinkedPackageGroupId ?? x.mp.MemberPackageId,
                        Price    = x.pd.Price ?? 0,
                    })
                    .ToListAsync();
                var mPkgIncome = mPkgRows.GroupBy(r => r.GroupKey).Sum(g => g.First().Price);

                var mManualQ = _db.ManualIncomeEntries
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted
                             && x.IncomeDate >= mStart && x.IncomeDate <= mEnd);
                if (branchFilterId.HasValue)
                    mManualQ = mManualQ.Where(x => x.BranchId == branchFilterId.Value);
                var mManualIncome = await mManualQ.SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var mExpQ = _db.Expenses
                    .Where(x => x.TenantId == tenantId && !x.IsDeleted
                             && x.ExpenseDate >= mStart && x.ExpenseDate <= mEnd);
                if (branchFilterId.HasValue)
                    mExpQ = mExpQ.Where(x => x.BranchId == branchFilterId.Value);
                var mExp = await mExpQ.SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var mNewMembers = await memberQuery
                    .CountAsync(m => m.CreatedAtUtc >= mStartDt && m.CreatedAtUtc <= mEndDt);

                monthlyTrend.Add(new MonthlyDataPoint
                {
                    MonthLabel = mStart.ToString("MMM yyyy"),
                    MonthStart = mStart,
                    Income     = mPkgIncome + mManualIncome,
                    Expenses   = mExp,
                    NewMembers = mNewMembers,
                });
            }
            vm.MonthlyTrend = monthlyTrend;

            return vm;
        }
    }
}
