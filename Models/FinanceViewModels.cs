using System.ComponentModel.DataAnnotations;

namespace GymSaaS.Models
{
    // ── Categories ───────────────────────────────────────────────────
    public static class ExpenseCategories
    {
        public static readonly Dictionary<string, string> All = new()
        {
            ["RENT"]         = "Rent",
            ["SALARIES"]     = "Salaries & Payroll",
            ["UTILITIES"]    = "Utilities",
            ["EQUIPMENT"]    = "Equipment",
            ["MARKETING"]    = "Marketing & Ads",
            ["MAINTENANCE"]  = "Maintenance & Repairs",
            ["INSURANCE"]    = "Insurance",
            ["TAXES"]        = "Taxes & Fees",
            ["SUPPLIES"]     = "Supplies & Consumables",
            ["OTHER"]        = "Other",
        };

        public static string DisplayName(string code) =>
            All.TryGetValue(code, out var n) ? n : code;
    }

    public static class IncomeCategories
    {
        public static readonly Dictionary<string, string> All = new()
        {
            ["DAY_PASS"]         = "Day Pass",
            ["PERSONAL_TRAINING"]= "Personal Training",
            ["MERCHANDISE"]      = "Merchandise",
            ["SUPPLEMENT"]       = "Supplements",
            ["INBODY"]           = "InBody Scans",
            ["OTHER"]            = "Other",
        };

        public static string DisplayName(string code) =>
            All.TryGetValue(code, out var n) ? n : code;
    }

    public static class PaymentMethods
    {
        public static readonly Dictionary<string, string> All = new()
        {
            ["CASH"]   = "Cash",
            ["CARD"]   = "Card",
            ["BANK"]   = "Bank Transfer",
            ["OTHER"]  = "Other",
        };
    }

    // ── Expense list item ────────────────────────────────────────────
    public class ExpenseListItem
    {
        public Guid ExpenseId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName => ExpenseCategories.DisplayName(CategoryCode);
        public string? VendorName { get; set; }
        public decimal Amount { get; set; }
        public DateOnly ExpenseDate { get; set; }
        public Guid? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    // ── Expense form ─────────────────────────────────────────────────
    public class ExpenseFormViewModel
    {
        public Guid? ExpenseId { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public string CategoryCode { get; set; } = string.Empty;

        public Guid? BranchId { get; set; }

        [MaxLength(200)]
        public string? VendorName { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 99999999, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public string? PaymentMethod { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public List<BranchDropdownItem> Branches { get; set; } = new();

        public bool IsEdit => ExpenseId.HasValue;
    }

    // ── Income (Manual) list item ────────────────────────────────────
    public class ManualIncomeListItem
    {
        public Guid IncomeEntryId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName => IncomeCategories.DisplayName(CategoryCode);
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateOnly IncomeDate { get; set; }
        public Guid? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ManualIncomeFormViewModel
    {
        public Guid? IncomeEntryId { get; set; }

        [Required(ErrorMessage = "Category is required")]
        public string CategoryCode { get; set; } = string.Empty;

        public Guid? BranchId { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required")]
        [Range(0.01, 99999999, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateOnly IncomeDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public string? PaymentMethod { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public List<BranchDropdownItem> Branches { get; set; } = new();

        public bool IsEdit => IncomeEntryId.HasValue;
    }

    // ── Reports ──────────────────────────────────────────────────────
    public class ReportsViewModel
    {
        // Date range
        public DateOnly RangeStart { get; set; }
        public DateOnly RangeEnd { get; set; }
        public string RangeLabel { get; set; } = string.Empty;
        public string RangePreset { get; set; } = "this_month";
        public Guid? BranchFilterId { get; set; }
        public string? BranchFilterName { get; set; }

        // Comparison (previous period of same length)
        public DateOnly ComparisonStart { get; set; }
        public DateOnly ComparisonEnd { get; set; }

        // Financial summary
        public decimal IncomeFromPackages { get; set; }
        public decimal IncomeFromManual { get; set; }
        public decimal TotalIncome => IncomeFromPackages + IncomeFromManual;
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit => TotalIncome - TotalExpenses;
        public decimal ProfitMarginPct => TotalIncome > 0 ? Math.Round((NetProfit / TotalIncome) * 100, 1) : 0;

        // Comparison numbers
        public decimal PrevIncome { get; set; }
        public decimal PrevExpenses { get; set; }
        public decimal PrevNet => PrevIncome - PrevExpenses;
        public decimal IncomeChangePct => PrevIncome > 0 ? Math.Round(((TotalIncome - PrevIncome) / PrevIncome) * 100, 1) : 0;
        public decimal ExpenseChangePct => PrevExpenses > 0 ? Math.Round(((TotalExpenses - PrevExpenses) / PrevExpenses) * 100, 1) : 0;
        public decimal NetChangePct => PrevNet != 0 ? Math.Round(((NetProfit - PrevNet) / Math.Abs(PrevNet)) * 100, 1) : 0;

        // Members summary
        public int TotalMembers { get; set; }       // All-time active members
        public int NewMembersInPeriod { get; set; }
        public int PrevPeriodNewMembers { get; set; }
        public decimal NewMembersChangePct =>
            PrevPeriodNewMembers > 0 ? Math.Round(((decimal)(NewMembersInPeriod - PrevPeriodNewMembers) / PrevPeriodNewMembers) * 100, 1) : 0;

        public int ActiveMembers { get; set; }      // Current ACTIVE status
        public int ChurnedMembers { get; set; }     // Went to SUSPENDED/INACTIVE in period

        // Attendance
        public int TotalCheckInsInPeriod { get; set; }
        public int PrevPeriodCheckIns { get; set; }
        public decimal CheckInsChangePct =>
            PrevPeriodCheckIns > 0 ? Math.Round(((decimal)(TotalCheckInsInPeriod - PrevPeriodCheckIns) / PrevPeriodCheckIns) * 100, 1) : 0;

        public double AvgDailyCheckIns => RangeEnd >= RangeStart
            ? Math.Round((double)TotalCheckInsInPeriod / (RangeEnd.DayNumber - RangeStart.DayNumber + 1), 1)
            : 0;

        // Monthly trend (last 6 months)
        public List<MonthlyDataPoint> MonthlyTrend { get; set; } = new();

        // Breakdowns
        public List<CategoryBreakdownItem> ExpenseByCategory { get; set; } = new();
        public List<CategoryBreakdownItem> IncomeByCategory { get; set; } = new();
        public List<BranchPerformanceItem> BranchPerformance { get; set; } = new();
        public List<TopPackageItem> TopSellingPackages { get; set; } = new();

        // Trend snapshot
        public string TrendIndicator =>
            IncomeChangePct > 5 ? "up" :
            IncomeChangePct < -5 ? "down" : "flat";
    }

    public class MonthlyDataPoint
    {
        public string MonthLabel { get; set; } = string.Empty;
        public DateOnly MonthStart { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net => Income - Expenses;
        public int NewMembers { get; set; }
    }

    public class CategoryBreakdownItem
    {
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public decimal Percent { get; set; }
    }

    public class BranchPerformanceItem
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net => Income - Expenses;
        public int NewMembers { get; set; }
        public int CheckIns { get; set; }
    }

    public class TopPackageItem
    {
        public string PackageName { get; set; } = string.Empty;
        public int AssignmentCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
