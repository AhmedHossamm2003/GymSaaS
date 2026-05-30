using GymSaaS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GymSaaS.Services.Exports
{
    public interface IPdfExportService
    {
        byte[] GenerateReport(ReportsViewModel report, string tenantName);
    }

    public class PdfExportService : IPdfExportService
    {
        // Brand colors
        private const string Orange     = "#f97316";
        private const string OrangeSoft = "#fff7ed";
        private const string Slate      = "#0f172a";
        private const string SlateLight = "#475569";
        private const string Muted      = "#94a3b8";
        private const string BorderClr  = "#e2e8f0";
        private const string GreenClr   = "#16a34a";
        private const string RedClr     = "#dc2626";

        public byte[] GenerateReport(ReportsViewModel r, string tenantName)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).FontColor(Slate));

                    // Header
                    page.Header().Element(header => BuildHeader(header, r, tenantName));

                    // Body
                    page.Content().PaddingTop(14).Column(col =>
                    {
                        col.Spacing(14);

                        // Financial summary
                        col.Item().Element(c => BuildFinancialSummary(c, r));

                        // Member analytics
                        col.Item().Element(c => BuildMemberAnalytics(c, r));

                        // Income by category
                        if (r.IncomeByCategory.Any())
                            col.Item().Element(c => BuildIncomeByCategory(c, r));

                        // Expense by category
                        if (r.ExpenseByCategory.Any())
                            col.Item().Element(c => BuildExpenseByCategory(c, r));

                        // Top selling packages
                        if (r.TopSellingPackages.Any())
                            col.Item().Element(c => BuildTopPackages(c, r));

                        // Per-branch breakdown
                        if (r.BranchPerformance.Any())
                            col.Item().Element(c => BuildBranchTable(c, r));

                        // Monthly trend
                        if (r.MonthlyTrend.Any())
                            col.Item().Element(c => BuildMonthlyTrend(c, r));
                    });

                    // Footer
                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Generated ").FontColor(Muted).FontSize(9);
                            text.Span($"{DateTime.Now:MMM d, yyyy h:mm tt}").FontColor(Muted).FontSize(9);
                        });
                        row.ConstantItem(80).AlignRight().Text(text =>
                        {
                            text.CurrentPageNumber().FontColor(Muted).FontSize(9);
                            text.Span(" / ").FontColor(Muted).FontSize(9);
                            text.TotalPages().FontColor(Muted).FontSize(9);
                        });
                    });
                });
            }).GeneratePdf();
        }

        private void BuildHeader(IContainer container, ReportsViewModel r, string tenantName)
        {
            container.BorderBottom(2).BorderColor(Orange).PaddingBottom(12).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(tenantName.ToUpper())
                        .FontSize(16).Bold().FontColor(Slate).LetterSpacing(0.04f);
                    col.Item().PaddingTop(2).Text("Performance Report")
                        .FontSize(11).FontColor(Orange).Bold();
                });

                row.ConstantItem(220).AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text(r.RangeLabel)
                        .FontSize(11).Bold().FontColor(Slate);
                    if (!string.IsNullOrEmpty(r.BranchFilterName))
                        col.Item().AlignRight().Text($"Branch: {r.BranchFilterName}")
                            .FontSize(9).FontColor(SlateLight);
                    col.Item().AlignRight().Text($"vs {r.ComparisonStart:MMM d} – {r.ComparisonEnd:MMM d}")
                        .FontSize(9).FontColor(Muted);
                });
            });
        }

        private void BuildFinancialSummary(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Financial Summary"));

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Element(box =>
                        StatBox(box, "TOTAL INCOME",  r.TotalIncome.ToString("N2"),
                                r.IncomeChangePct, Orange));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "TOTAL EXPENSES", r.TotalExpenses.ToString("N2"),
                                r.ExpenseChangePct, RedClr, expenseInverted: true));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "NET PROFIT", r.NetProfit.ToString("N2"),
                                r.NetChangePct, GreenClr));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "PROFIT MARGIN", $"{r.ProfitMarginPct}%",
                                null, Slate));
                });

                // Income breakdown row
                col.Item().PaddingTop(8).Border(1).BorderColor(BorderClr).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c2 =>
                    {
                        c2.Item().Text("Package Sales").FontSize(9).FontColor(Muted).Bold();
                        c2.Item().PaddingTop(2).Text(r.IncomeFromPackages.ToString("N2"))
                            .FontSize(12).Bold().FontColor(Slate);
                    });
                    row.RelativeItem().Column(c2 =>
                    {
                        c2.Item().Text("Manual Income").FontSize(9).FontColor(Muted).Bold();
                        c2.Item().PaddingTop(2).Text(r.IncomeFromManual.ToString("N2"))
                            .FontSize(12).Bold().FontColor(Slate);
                    });
                });
            });
        }

        private void BuildMemberAnalytics(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Member Analytics"));

                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Element(box =>
                        StatBox(box, "TOTAL MEMBERS", r.TotalMembers.ToString("N0"), null, Slate));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "ACTIVE NOW", r.ActiveMembers.ToString("N0"), null, GreenClr));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "NEW IN PERIOD", r.NewMembersInPeriod.ToString("N0"),
                                r.NewMembersChangePct, Orange));

                    row.ConstantItem(8);

                    row.RelativeItem().Element(box =>
                        StatBox(box, "CHECK-INS", r.TotalCheckInsInPeriod.ToString("N0"),
                                r.CheckInsChangePct, Slate));
                });
            });
        }

        private void BuildIncomeByCategory(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Income by Category"));

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    TableHeader(table, "Category", "Count", "Amount", "% of Total");

                    foreach (var c2 in r.IncomeByCategory)
                    {
                        TableRow(table,
                            c2.CategoryName,
                            c2.Count.ToString(),
                            c2.Amount.ToString("N2"),
                            $"{c2.Percent}%");
                    }
                });
            });
        }

        private void BuildExpenseByCategory(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Expenses by Category"));

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    TableHeader(table, "Category", "Count", "Amount", "% of Total");

                    foreach (var c2 in r.ExpenseByCategory)
                    {
                        TableRow(table,
                            c2.CategoryName,
                            c2.Count.ToString(),
                            c2.Amount.ToString("N2"),
                            $"{c2.Percent}%");
                    }
                });
            });
        }

        private void BuildTopPackages(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Top Selling Packages"));

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                    });

                    TableHeader(table, "Package", "Sales", "Revenue");

                    foreach (var p in r.TopSellingPackages)
                    {
                        TableRow(table,
                            p.PackageName,
                            p.AssignmentCount.ToString(),
                            p.TotalRevenue.ToString("N2"));
                    }
                });
            });
        }

        private void BuildBranchTable(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Branch Performance"));

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    TableHeader(table, "Branch", "Income", "Expenses", "Net", "New", "Visits");

                    foreach (var b in r.BranchPerformance)
                    {
                        TableRow(table,
                            b.BranchName,
                            b.Income.ToString("N2"),
                            b.Expenses.ToString("N2"),
                            b.Net.ToString("N2"),
                            b.NewMembers.ToString(),
                            b.CheckIns.ToString());
                    }
                });
            });
        }

        private void BuildMonthlyTrend(IContainer c, ReportsViewModel r)
        {
            c.Column(col =>
            {
                col.Item().Element(SectionTitle("Monthly Trend (last 6 months)"));

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                    });

                    TableHeader(table, "Month", "Income", "Expenses", "Net", "New Members");

                    foreach (var m in r.MonthlyTrend)
                    {
                        TableRow(table,
                            m.MonthLabel,
                            m.Income.ToString("N2"),
                            m.Expenses.ToString("N2"),
                            m.Net.ToString("N2"),
                            m.NewMembers.ToString());
                    }
                });
            });
        }

        // ── Helpers ──────────────────────────────────────────────────
        private Action<IContainer> SectionTitle(string title) => c =>
            c.PaddingBottom(2).BorderBottom(1).BorderColor(BorderClr).Text(title)
                .FontSize(11).Bold().FontColor(Slate).LetterSpacing(0.03f);

        private void StatBox(IContainer c, string label, string value,
                             decimal? changePct, string accent, bool expenseInverted = false)
        {
            c.Border(1).BorderColor(BorderClr).Padding(10).Column(col =>
            {
                col.Item().Text(label).FontSize(8).Bold().FontColor(Muted).LetterSpacing(0.06f);
                col.Item().PaddingTop(4).Text(value).FontSize(14).Bold().FontColor(accent);

                if (changePct.HasValue)
                {
                    var sign = changePct.Value >= 0 ? "+" : "";
                    // For expenses: up = bad (red), down = good (green)
                    bool isPositive = expenseInverted ? changePct.Value < 0 : changePct.Value > 0;
                    var color = changePct.Value == 0 ? Muted : (isPositive ? GreenClr : RedClr);
                    col.Item().PaddingTop(2).Text($"{sign}{changePct.Value}% vs prev")
                        .FontSize(8).FontColor(color);
                }
            });
        }

        private void TableHeader(TableDescriptor table, params string[] headers)
        {
            table.Header(h =>
            {
                foreach (var col in headers)
                {
                    h.Cell().Background(OrangeSoft).BorderBottom(1).BorderColor(BorderClr)
                        .Padding(6).Text(col)
                        .FontSize(8).Bold().FontColor(Slate).LetterSpacing(0.06f);
                }
            });
        }

        private void TableRow(TableDescriptor table, params string[] cells)
        {
            foreach (var v in cells)
            {
                table.Cell().BorderBottom(1).BorderColor(BorderClr).Padding(6).Text(v)
                    .FontSize(9).FontColor(Slate);
            }
        }
    }
}
