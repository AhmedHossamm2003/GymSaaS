using ClosedXML.Excel;
using GymSaaS.Models;

namespace GymSaaS.Services.Exports
{
    public interface IExcelExportService
    {
        byte[] GenerateReport(ReportsViewModel report, string tenantName);
    }

    public class ExcelExportService : IExcelExportService
    {
        private static readonly XLColor Orange     = XLColor.FromHtml("#f97316");
        private static readonly XLColor OrangeSoft = XLColor.FromHtml("#fff7ed");
        private static readonly XLColor Slate      = XLColor.FromHtml("#0f172a");
        private static readonly XLColor BorderClr  = XLColor.FromHtml("#e2e8f0");
        private static readonly XLColor MutedClr   = XLColor.FromHtml("#94a3b8");
        private static readonly XLColor GreenClr   = XLColor.FromHtml("#16a34a");
        private static readonly XLColor RedClr     = XLColor.FromHtml("#dc2626");

        public byte[] GenerateReport(ReportsViewModel r, string tenantName)
        {
            using var wb = new XLWorkbook();

            BuildSummarySheet(wb, r, tenantName);
            BuildIncomeSheet(wb, r);
            BuildExpensesSheet(wb, r);
            BuildBranchSheet(wb, r);
            BuildMonthlyTrendSheet(wb, r);
            BuildTopPackagesSheet(wb, r);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ─── SUMMARY SHEET ───────────────────────────────────────────
        private void BuildSummarySheet(XLWorkbook wb, ReportsViewModel r, string tenantName)
        {
            var s = wb.Worksheets.Add("Summary");
            s.ColumnWidth = 22;

            // Title block
            s.Range("A1:E1").Merge();
            s.Cell("A1").Value = $"{tenantName.ToUpper()} — Performance Report";
            s.Cell("A1").Style.Font.Bold = true;
            s.Cell("A1").Style.Font.FontSize = 16;
            s.Cell("A1").Style.Font.FontColor = Slate;

            s.Range("A2:E2").Merge();
            s.Cell("A2").Value = $"Period: {r.RangeLabel}";
            s.Cell("A2").Style.Font.FontColor = Orange;
            s.Cell("A2").Style.Font.Bold = true;

            s.Range("A3:E3").Merge();
            s.Cell("A3").Value = $"vs {r.ComparisonStart:MMM d, yyyy} – {r.ComparisonEnd:MMM d, yyyy}";
            s.Cell("A3").Style.Font.FontColor = MutedClr;

            if (!string.IsNullOrEmpty(r.BranchFilterName))
            {
                s.Range("A4:E4").Merge();
                s.Cell("A4").Value = $"Branch: {r.BranchFilterName}";
                s.Cell("A4").Style.Font.Italic = true;
            }

            // Financial summary
            int row = 6;
            row = WriteSectionHeader(s, row, "FINANCIAL SUMMARY");
            row = WriteSummaryKV(s, row, "Total Income",     r.TotalIncome,    "currency");
            row = WriteSummaryKV(s, row, "  Package Sales",  r.IncomeFromPackages, "currency");
            row = WriteSummaryKV(s, row, "  Manual Income",  r.IncomeFromManual,   "currency");
            row = WriteSummaryKV(s, row, "Total Expenses",   r.TotalExpenses,  "currency");
            row = WriteSummaryKV(s, row, "Net Profit",       r.NetProfit,      "currency", bold: true);
            row = WriteSummaryKV(s, row, "Profit Margin",    r.ProfitMarginPct, "percent");
            row++;

            // Trend comparison
            row = WriteSectionHeader(s, row, "VS PREVIOUS PERIOD");
            row = WriteSummaryKV(s, row, "Income Change",   r.IncomeChangePct,   "percent_delta");
            row = WriteSummaryKV(s, row, "Expense Change",  r.ExpenseChangePct,  "percent_delta_inverted");
            row = WriteSummaryKV(s, row, "Net Change",      r.NetChangePct,      "percent_delta");
            row++;

            // Member analytics
            row = WriteSectionHeader(s, row, "MEMBER ANALYTICS");
            row = WriteSummaryKV(s, row, "Total Members",      r.TotalMembers, "number");
            row = WriteSummaryKV(s, row, "Active Now",         r.ActiveMembers, "number");
            row = WriteSummaryKV(s, row, "New This Period",    r.NewMembersInPeriod, "number");
            row = WriteSummaryKV(s, row, "New Last Period",    r.PrevPeriodNewMembers, "number");
            row = WriteSummaryKV(s, row, "Growth %",           r.NewMembersChangePct, "percent_delta");
            row = WriteSummaryKV(s, row, "Churned",            r.ChurnedMembers, "number");
            row++;

            // Attendance
            row = WriteSectionHeader(s, row, "ATTENDANCE");
            row = WriteSummaryKV(s, row, "Total Check-Ins",      r.TotalCheckInsInPeriod, "number");
            row = WriteSummaryKV(s, row, "Previous Period",      r.PrevPeriodCheckIns, "number");
            row = WriteSummaryKV(s, row, "Change %",             r.CheckInsChangePct, "percent_delta");
            row = WriteSummaryKV(s, row, "Avg Daily",            (decimal)r.AvgDailyCheckIns, "number");

            // Column sizing
            s.Columns(1, 2).AdjustToContents();
            s.Column(1).Width = Math.Max(s.Column(1).Width, 24);
            s.Column(2).Width = Math.Max(s.Column(2).Width, 18);
        }

        // ─── INCOME SHEET ────────────────────────────────────────────
        private void BuildIncomeSheet(XLWorkbook wb, ReportsViewModel r)
        {
            var s = wb.Worksheets.Add("Income");
            s.Cell(1, 1).Value = "Income by Category";
            s.Range(1, 1, 1, 4).Merge();
            s.Cell(1, 1).Style.Font.Bold = true;
            s.Cell(1, 1).Style.Font.FontSize = 13;
            s.Cell(1, 1).Style.Font.FontColor = Slate;

            WriteTableHeader(s, 3, "Category", "Count", "Amount", "% of Total");
            int row = 4;
            foreach (var c in r.IncomeByCategory)
            {
                s.Cell(row, 1).Value = c.CategoryName;
                s.Cell(row, 2).Value = c.Count;
                s.Cell(row, 3).Value = c.Amount;
                s.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                s.Cell(row, 4).Value = (double)c.Percent / 100;
                s.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            // Total row
            if (r.IncomeByCategory.Any())
            {
                s.Cell(row, 1).Value = "TOTAL";
                s.Cell(row, 3).FormulaA1 = $"SUM(C4:C{row - 1})";
                s.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                s.Range(row, 1, row, 4).Style.Font.Bold = true;
                s.Range(row, 1, row, 4).Style.Fill.BackgroundColor = OrangeSoft;
                s.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            s.Columns(1, 4).AdjustToContents();
        }

        // ─── EXPENSES SHEET ──────────────────────────────────────────
        private void BuildExpensesSheet(XLWorkbook wb, ReportsViewModel r)
        {
            var s = wb.Worksheets.Add("Expenses");
            s.Cell(1, 1).Value = "Expenses by Category";
            s.Range(1, 1, 1, 4).Merge();
            s.Cell(1, 1).Style.Font.Bold = true;
            s.Cell(1, 1).Style.Font.FontSize = 13;
            s.Cell(1, 1).Style.Font.FontColor = Slate;

            WriteTableHeader(s, 3, "Category", "Count", "Amount", "% of Total");
            int row = 4;
            foreach (var c in r.ExpenseByCategory)
            {
                s.Cell(row, 1).Value = c.CategoryName;
                s.Cell(row, 2).Value = c.Count;
                s.Cell(row, 3).Value = c.Amount;
                s.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                s.Cell(row, 4).Value = (double)c.Percent / 100;
                s.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            if (r.ExpenseByCategory.Any())
            {
                s.Cell(row, 1).Value = "TOTAL";
                s.Cell(row, 3).FormulaA1 = $"SUM(C4:C{row - 1})";
                s.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                s.Range(row, 1, row, 4).Style.Font.Bold = true;
                s.Range(row, 1, row, 4).Style.Fill.BackgroundColor = OrangeSoft;
                s.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            s.Columns(1, 4).AdjustToContents();
        }

        // ─── BRANCH SHEET ────────────────────────────────────────────
        private void BuildBranchSheet(XLWorkbook wb, ReportsViewModel r)
        {
            var s = wb.Worksheets.Add("By Branch");
            s.Cell(1, 1).Value = "Branch Performance";
            s.Range(1, 1, 1, 6).Merge();
            s.Cell(1, 1).Style.Font.Bold = true;
            s.Cell(1, 1).Style.Font.FontSize = 13;
            s.Cell(1, 1).Style.Font.FontColor = Slate;

            WriteTableHeader(s, 3, "Branch", "Income", "Expenses", "Net", "New Members", "Check-Ins");
            int row = 4;
            foreach (var b in r.BranchPerformance)
            {
                s.Cell(row, 1).Value = b.BranchName;
                s.Cell(row, 2).Value = b.Income;
                s.Cell(row, 3).Value = b.Expenses;
                s.Cell(row, 4).Value = b.Net;
                s.Cell(row, 5).Value = b.NewMembers;
                s.Cell(row, 6).Value = b.CheckIns;
                s.Range(row, 2, row, 4).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            s.Columns(1, 6).AdjustToContents();
        }

        // ─── MONTHLY TREND SHEET ─────────────────────────────────────
        private void BuildMonthlyTrendSheet(XLWorkbook wb, ReportsViewModel r)
        {
            var s = wb.Worksheets.Add("Monthly Trend");
            s.Cell(1, 1).Value = "Monthly Trend (last 6 months)";
            s.Range(1, 1, 1, 5).Merge();
            s.Cell(1, 1).Style.Font.Bold = true;
            s.Cell(1, 1).Style.Font.FontSize = 13;
            s.Cell(1, 1).Style.Font.FontColor = Slate;

            WriteTableHeader(s, 3, "Month", "Income", "Expenses", "Net", "New Members");
            int row = 4;
            foreach (var m in r.MonthlyTrend)
            {
                s.Cell(row, 1).Value = m.MonthLabel;
                s.Cell(row, 2).Value = m.Income;
                s.Cell(row, 3).Value = m.Expenses;
                s.Cell(row, 4).Value = m.Net;
                s.Cell(row, 5).Value = m.NewMembers;
                s.Range(row, 2, row, 4).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            s.Columns(1, 5).AdjustToContents();
        }

        // ─── TOP PACKAGES SHEET ──────────────────────────────────────
        private void BuildTopPackagesSheet(XLWorkbook wb, ReportsViewModel r)
        {
            var s = wb.Worksheets.Add("Top Packages");
            s.Cell(1, 1).Value = "Top Selling Packages";
            s.Range(1, 1, 1, 3).Merge();
            s.Cell(1, 1).Style.Font.Bold = true;
            s.Cell(1, 1).Style.Font.FontSize = 13;
            s.Cell(1, 1).Style.Font.FontColor = Slate;

            WriteTableHeader(s, 3, "Package", "Sales Count", "Total Revenue");
            int row = 4;
            foreach (var p in r.TopSellingPackages)
            {
                s.Cell(row, 1).Value = p.PackageName;
                s.Cell(row, 2).Value = p.AssignmentCount;
                s.Cell(row, 3).Value = p.TotalRevenue;
                s.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            s.Columns(1, 3).AdjustToContents();
        }

        // ─── HELPERS ─────────────────────────────────────────────────
        private int WriteSectionHeader(IXLWorksheet s, int row, string title)
        {
            s.Cell(row, 1).Value = title;
            s.Range(row, 1, row, 2).Merge();
            s.Cell(row, 1).Style.Font.Bold = true;
            s.Cell(row, 1).Style.Font.FontColor = Slate;
            s.Cell(row, 1).Style.Fill.BackgroundColor = OrangeSoft;
            s.Cell(row, 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            s.Cell(row, 1).Style.Border.BottomBorderColor = Orange;
            return row + 1;
        }

        private int WriteSummaryKV(IXLWorksheet s, int row, string label, decimal value, string format, bool bold = false)
        {
            s.Cell(row, 1).Value = label;
            s.Cell(row, 2).Value = value;

            switch (format)
            {
                case "currency":
                    s.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                    break;
                case "number":
                    s.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
                    break;
                case "percent":
                    s.Cell(row, 2).Style.NumberFormat.Format = "0.0\\%";
                    break;
                case "percent_delta":
                    s.Cell(row, 2).Style.NumberFormat.Format = "+0.0\\%;-0.0\\%;0\\%";
                    s.Cell(row, 2).Style.Font.FontColor = value > 0 ? GreenClr : value < 0 ? RedClr : MutedClr;
                    break;
                case "percent_delta_inverted":
                    s.Cell(row, 2).Style.NumberFormat.Format = "+0.0\\%;-0.0\\%;0\\%";
                    s.Cell(row, 2).Style.Font.FontColor = value < 0 ? GreenClr : value > 0 ? RedClr : MutedClr;
                    break;
            }

            if (bold)
            {
                s.Range(row, 1, row, 2).Style.Font.Bold = true;
                s.Range(row, 1, row, 2).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            return row + 1;
        }

        private void WriteTableHeader(IXLWorksheet s, int row, params string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = s.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = OrangeSoft;
                cell.Style.Font.FontColor = Slate;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                cell.Style.Border.BottomBorderColor = Orange;
            }
        }
    }
}
