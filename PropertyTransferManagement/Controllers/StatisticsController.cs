using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.Services.Reporting;

namespace PropertyTransferManagement.Controllers
{
    public record ChartPoint(string Label, decimal Value);

    public class OfficerStatsRow
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ActiveCases { get; set; }
        public int CompletedCases { get; set; }
        public int PendingReviews { get; set; }
        public double? AverageDays { get; set; }
    }

    public class StatisticsViewModel
    {
        public int TotalCases { get; set; }
        public int NewThisMonth { get; set; }
        public int Pending { get; set; }
        public int WaitingDocuments { get; set; }
        public int UnderReview { get; set; }
        public int Completed { get; set; }
        public int CancelledOrRejected { get; set; }
        public double? AverageProcessingDays { get; set; }

        public decimal Revenue { get; set; }
        public int SucceededPayments { get; set; }
        public int PendingPayments { get; set; }

        public int LoanTotal { get; set; }
        public int LoanAwaitingDocuments { get; set; }
        public int LoanUnderReview { get; set; }
        public int LoanSubmittedToLender { get; set; }
        public int LoanDecisionsRecorded { get; set; }

        public List<OfficerStatsRow> Officers { get; set; } = new();
        public List<ChartPoint> CasesPerMonth { get; set; } = new();
        public List<ChartPoint> CasesByStatus { get; set; } = new();
        public List<ChartPoint> RevenuePerMonth { get; set; } = new();
        public List<ChartPoint> RevenueByService { get; set; } = new();
        public List<ChartPoint> LoansByStatus { get; set; } = new();
    }

    /// <summary>Admin statistics with Chart.js and PDF/Excel exports (spec §33).</summary>
    [Authorize(Roles = DataSeeder.AdminRole)]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public StatisticsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var since = monthStart.AddMonths(-11);
            var cases = _db.TransferCases.AsNoTracking();

            var vm = new StatisticsViewModel
            {
                TotalCases = await cases.CountAsync(),
                NewThisMonth = await cases.CountAsync(c => c.CreatedAt >= monthStart),
                Pending = await cases.CountAsync(c => c.Status == TransferCaseStatus.RequestReceived || c.AssignedOfficerId == null),
                WaitingDocuments = await cases.CountAsync(c => c.Status == TransferCaseStatus.AwaitingDocuments ||
                                                               c.Status == TransferCaseStatus.AdditionalInformationRequired),
                UnderReview = await cases.CountAsync(c => c.Status == TransferCaseStatus.DocumentsUnderReview),
                Completed = await cases.CountAsync(c => c.Status == TransferCaseStatus.OfficialCompletionConfirmed ||
                                                        c.Status == TransferCaseStatus.CaseClosed),
                CancelledOrRejected = await cases.CountAsync(c => c.Status == TransferCaseStatus.Cancelled ||
                                                                  c.Status == TransferCaseStatus.Rejected),

                Revenue = await _db.ServicePayments.Where(p => p.Status == ServicePaymentStatus.Succeeded).SumAsync(p => (decimal?)p.Amount) ?? 0,
                SucceededPayments = await _db.ServicePayments.CountAsync(p => p.Status == ServicePaymentStatus.Succeeded),
                PendingPayments = await _db.ServicePayments.CountAsync(p => p.Status == ServicePaymentStatus.Pending ||
                                                                           p.Status == ServicePaymentStatus.Failed),

                LoanTotal = await _db.LoanAssistanceRequests.CountAsync(),
                LoanAwaitingDocuments = await _db.LoanAssistanceRequests.CountAsync(r => r.Status == LoanAssistanceStatus.InformationRequested),
                LoanUnderReview = await _db.LoanAssistanceRequests.CountAsync(r => r.Status == LoanAssistanceStatus.UnderReview ||
                                                                                 r.Status == LoanAssistanceStatus.Pending),
                LoanSubmittedToLender = await _db.LoanAssistanceRequests.CountAsync(r => r.Status == LoanAssistanceStatus.ReferredToBank),
                LoanDecisionsRecorded = await _db.LoanAssistanceRequests.CountAsync(r => r.LenderDecision != null)
            };

            var completed = await cases.Where(c => c.CompletedAt != null)
                .Select(c => new { c.CreatedAt, c.CompletedAt, c.AssignedOfficerId }).ToListAsync();
            if (completed.Count > 0)
                vm.AverageProcessingDays = Math.Round(completed.Average(c => (c.CompletedAt!.Value - c.CreatedAt).TotalDays), 1);

            // Officers
            var officers = await _userManager.GetUsersInRoleAsync(DataSeeder.OfficerRole);
            var active = await cases.Where(c => c.AssignedOfficerId != null && c.CompletedAt == null &&
                                                c.Status != TransferCaseStatus.Cancelled && c.Status != TransferCaseStatus.Rejected)
                .GroupBy(c => c.AssignedOfficerId!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var pendingReviews = await _db.CaseDocuments
                .Where(d => !d.IsDeleted && d.Status == CaseDocumentStatus.PendingReview && d.TransferCase.AssignedOfficerId != null)
                .GroupBy(d => d.TransferCase.AssignedOfficerId!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

            vm.Officers = officers.Select(o =>
            {
                var mine = completed.Where(c => c.AssignedOfficerId == o.Id).ToList();
                return new OfficerStatsRow
                {
                    Name = $"{o.FirstName} {o.LastName}".Trim(),
                    Email = o.Email ?? "",
                    ActiveCases = active.GetValueOrDefault(o.Id),
                    CompletedCases = mine.Count,
                    PendingReviews = pendingReviews.GetValueOrDefault(o.Id),
                    AverageDays = mine.Count == 0 ? null : Math.Round(mine.Average(c => (c.CompletedAt!.Value - c.CreatedAt).TotalDays), 1)
                };
            }).OrderByDescending(o => o.ActiveCases).ToList();

            // Charts
            var perMonth = await cases.Where(c => c.CreatedAt >= since)
                .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();
            vm.CasesPerMonth = Months(since, (y, m) => perMonth.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            vm.CasesByStatus = (await cases.GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .OrderBy(x => x.Key).Select(x => new ChartPoint(CaseWorkflow.Display(x.Key), x.Count)).ToList();

            var revenue = await _db.ServicePayments.Where(p => p.Status == ServicePaymentStatus.Succeeded && p.PaidAt >= since)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Sum = g.Sum(p => p.Amount) }).ToListAsync();
            vm.RevenuePerMonth = Months(since, (y, m) => revenue.FirstOrDefault(x => x.Year == y && x.Month == m)?.Sum ?? 0);

            vm.RevenueByService = (await _db.ServicePayments.Where(p => p.Status == ServicePaymentStatus.Succeeded)
                    .GroupBy(p => p.ServiceName).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToListAsync())
                .Select(x => new ChartPoint(x.Key, x.Sum)).ToList();

            vm.LoansByStatus = (await _db.LoanAssistanceRequests.GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .Select(x => new ChartPoint(CaseExtrasDisplay.Loan(x.Key), x.Count)).ToList();

            return View(vm);
        }

        // ---------- Exports: ?format=xlsx (default) or pdf ----------

        public async Task<IActionResult> ExportCases(string? format)
        {
            var rows = await _db.TransferCases.AsNoTracking().Include(c => c.AssignedOfficer)
                .OrderByDescending(c => c.CreatedAt).ToListAsync();
            var report = new TableReport
            {
                Title = "Transfer cases",
                Columns = new[] { "Case", "Property", "City", "Buyer", "Seller", "Price", "Currency", "Status", "Officer", "Opened", "Completed" }
            };
            report.Summary.Add(("Cases", rows.Count.ToString()));
            foreach (var c in rows)
                report.Rows.Add(new object?[] { c.CaseNumber, c.PropertyTitle, c.City, c.BuyerFullName, c.SellerFullName, c.AgreedPrice,
                    c.Currency, CaseWorkflow.Display(c.Status), c.AssignedOfficer?.Email, c.CreatedAt, c.CompletedAt });
            return Send(report, format, "transfer-cases");
        }

        public async Task<IActionResult> ExportOfficers(string? format)
        {
            var vm = (StatisticsViewModel)((ViewResult)await Index()).Model!;
            var report = new TableReport
            {
                Title = "Officer statistics",
                Columns = new[] { "Officer", "Email", "Active cases", "Completed cases", "Documents to review", "Average days to complete" }
            };
            foreach (var o in vm.Officers)
                report.Rows.Add(new object?[] { o.Name, o.Email, o.ActiveCases, o.CompletedCases, o.PendingReviews, o.AverageDays });
            return Send(report, format, "officers");
        }

        public async Task<IActionResult> ExportPayments(string? format)
        {
            var rows = await _db.ServicePayments.AsNoTracking().Include(p => p.TransferCase).Include(p => p.PaidByUser)
                .OrderByDescending(p => p.CreatedAt).ToListAsync();
            var report = new TableReport
            {
                Title = "Service payments",
                Subtitle = $"Assistance service fees (not government fees) · generated {DateTime.Now:g}",
                Columns = new[] { "Id", "Date", "Case", "Paid by", "Service", "Amount", "Currency", "Status", "Reference" }
            };
            report.Summary.Add(("Revenue (succeeded)", rows.Where(p => p.Status == ServicePaymentStatus.Succeeded).Sum(p => p.Amount).ToString("N2") + " USD"));
            foreach (var p in rows)
                report.Rows.Add(new object?[] { p.Id, p.CreatedAt, p.TransferCase.CaseNumber, p.PaidByUser.Email, p.ServiceName, p.Amount,
                    p.Currency, p.Status.ToString(), p.ProviderTransactionId ?? p.FailureReason });
            return Send(report, format, "service-payments");
        }

        public async Task<IActionResult> ExportLoans(string? format)
        {
            var rows = await _db.LoanAssistanceRequests.AsNoTracking().Include(r => r.TransferCase).Include(r => r.RequestedByUser)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();
            var report = new TableReport
            {
                Title = "Loan-assistance requests",
                Columns = new[] { "Case", "Buyer", "Amount", "Currency", "Status", "Lender", "Lender decision", "Created", "Updated" }
            };
            foreach (var r in rows)
                report.Rows.Add(new object?[] { r.TransferCase.CaseNumber, r.RequestedByUser.Email, r.RequestedAmount, r.Currency,
                    CaseExtrasDisplay.Loan(r.Status), r.LenderName, CaseExtrasDisplay.Lender(r.LenderDecision), r.CreatedAt, r.UpdatedAt });
            return Send(report, format, "loan-assistance");
        }

        private FileContentResult Send(TableReport report, string? format, string name)
        {
            var file = ReportExporter.Export(report, format, name);
            return File(file.Content, file.ContentType, file.FileName);
        }

        private static List<ChartPoint> Months(DateTime since, Func<int, int, decimal> valueFor)
        {
            var list = new List<ChartPoint>();
            for (int i = 0; i < 12; i++)
            {
                var m = since.AddMonths(i);
                list.Add(new ChartPoint(m.ToString("MMM yy", CultureInfo.InvariantCulture), valueFor(m.Year, m.Month)));
            }
            return list;
        }
    }
}
